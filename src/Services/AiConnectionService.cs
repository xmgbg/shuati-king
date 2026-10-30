using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShuatiKing.Models;

namespace ShuatiKing.Services;

// 状态码说明表（收到 HTTP 响应时始终保留实际状态码，未列出的也原样展示）：
// HTTP 2xx：仅收到有效 assistant 文本才算成功；空内容、错误结构使用 LOCAL_RESPONSE_INVALID。
// HTTP 400 / 422：请求参数不符合要求，检查服务地址、模型和服务说明。
// HTTP 401：鉴权失败，检查 API 密钥。403：访问被拒绝，检查账号与模型权限。
// HTTP 404：资源不存在，检查接口路径和模型名称。408：服务端超时，稍后重试。
// HTTP 429：请求受限，检查额度或稍后重试。
// HTTP 500 / 502 / 503 / 504：服务或上游异常，可稍后重试。
// LOCAL_CONFIG_INVALID：配置不完整或地址不合法，修正配置后重试。
// LOCAL_TIMEOUT：客户端等待超时，检查网络后重试；已收到的 HTTP 状态码仍保留。
// LOCAL_NETWORK_ERROR：网络、DNS、TLS 或传输失败，检查网络与地址，不伪造 HTTP 状态码。
// LOCAL_RESPONSE_INVALID：响应不是有效模型结果，检查接口兼容性，并保留实际 HTTP 状态码。
// 用户主动离开页面时取消请求，不将取消伪装成超时或网络错误。
public sealed class AiConnectionService(HttpClient? client = null)
{
    private static readonly HttpClient SharedClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    { Timeout = Timeout.InfiniteTimeSpan };
    private readonly HttpClient http = client ?? SharedClient;

    public static Uri CompletionUri(string endpoint)
    {
        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("请填写不含账号、查询参数或片段的 http:// 或 https:// API 地址。");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) path += "/chat/completions";
        return new UriBuilder(uri) { Path = path }.Uri;
    }

    public async Task<ConnectionCheckResult> CheckAsync(AiConfiguration configuration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = configuration.ApiKey.Trim();
        Uri uri;
        try { uri = CompletionUri(configuration.Endpoint); }
        catch (ArgumentException error) { return Failed(null, "LOCAL_CONFIG_INVALID", error.Message); }
        if (string.IsNullOrWhiteSpace(configuration.Model) || string.IsNullOrWhiteSpace(key) || key.Contains('\r') || key.Contains('\n'))
            return Failed(null, "LOCAL_CONFIG_INVALID", "请填写有效的 API 密钥和模型名称。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        int? status = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = configuration.Model.Trim(),
                messages = new[] { new { role = "user", content = "Reply only with OK." } },
                stream = false
            }), Encoding.UTF8, "application/json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            JsonDocument? document = null;
            try { document = JsonDocument.Parse(body); }
            catch (JsonException) { }
            using (document)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var detail = "";
                    if (document?.RootElement is { ValueKind: JsonValueKind.Object } root &&
                        root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object &&
                        error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                        detail = Redact(message.GetString()!, key);
                    return Failed(status, null, StatusHint(status.Value) + (detail.Length > 0 ? "\n服务说明：" + detail : ""));
                }
                if (document?.RootElement is { ValueKind: JsonValueKind.Object } result &&
                    result.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0 &&
                    choices[0].ValueKind == JsonValueKind.Object && choices[0].TryGetProperty("message", out var answer) && answer.ValueKind == JsonValueKind.Object &&
                    answer.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String && role.GetString() == "assistant" &&
                    answer.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(content.GetString()))
                    return new(true, $"连接成功\nHTTP {status} · 已收到有效模型响应。");
                return Failed(status, "LOCAL_RESPONSE_INVALID", "未收到有效模型文本，请检查接口兼容性及模型配置后重试。");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException)
        { return Failed(status, "LOCAL_TIMEOUT", "等待模型响应超过时限，请检查网络或稍后重试。"); }
        catch (Exception error) when (error is HttpRequestException or IOException)
        { return Failed(status, "LOCAL_NETWORK_ERROR", "请求传输失败，请检查网络、服务地址及 TLS 连接后重试。"); }
    }

    private static string Redact(string value, string key)
    {
        var safe = value.Replace(key, "[已隐藏]", StringComparison.Ordinal);
        safe = Regex.Replace(safe, @"Bearer\s+[^\s""',;]+", "Bearer [已隐藏]", RegexOptions.IgnoreCase);
        safe = Regex.Replace(safe, @"\bsk-[A-Za-z0-9_.-]+", "[已隐藏]");
        safe = Regex.Replace(safe, @"\s+", " ").Trim();
        return safe.Length > 300 ? safe[..300] + "…" : safe;
    }

    private static ConnectionCheckResult Failed(int? status, string? code, string description)
    {
        var label = status is null ? "" : $"HTTP {status}";
        if (code is not null) label += (label.Length > 0 ? " · " : "") + code + "（本地错误码）";
        return new(false, $"连接失败\n{label} · {description}");
    }

    private static string StatusHint(int status) => status switch
    {
        400 or 422 => "请求参数不符合服务要求，请检查地址、模型及服务说明。",
        401 => "鉴权失败，请检查 API 密钥。",
        403 => "访问被拒绝，请检查账号与模型权限。",
        404 => "资源不存在，请检查服务地址、接口路径和模型名称。",
        408 => "服务端请求超时，请稍后重试。",
        429 => "请求受限，请检查服务额度或稍后重试。",
        500 or 502 or 503 or 504 => "服务或上游暂时异常，请稍后重试。",
        >= 300 and < 400 => "服务返回重定向，请直接填写服务提供方的正确 API 地址。",
        _ => "服务拒绝或未能完成请求，请检查配置及服务说明。"
    };
}

public sealed record ConnectionCheckResult(bool Success, string Message);
