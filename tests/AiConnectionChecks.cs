using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ShuatiKing.Models;
using ShuatiKing.Services;

internal static class AiConnectionChecks
{
    internal static void Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var configuration = new AiConfiguration { Endpoint = "https://api.example.test/v1/", ApiKey = "sk-test-secret-123", Model = "test-model" };
        var paths = new[]
        {
            ("https://api.deepseek.com", "https://api.deepseek.com/chat/completions"),
            ("https://api.xiaomimimo.com/v1/", "https://api.xiaomimimo.com/v1/chat/completions"),
            ("https://api.example.test/v1/chat/completions/", "https://api.example.test/v1/chat/completions")
        };
        foreach (var (input, output) in paths)
            Program.Check(AiConnectionService.CompletionUri(input).AbsoluteUri == output, "基础地址及完整接口地址不会重复追加路径");
        using var successClient = new HttpClient(new Handler(async (request, token) =>
        {
            Program.Check(request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/v1/chat/completions", "真实检查使用 Chat Completions POST 路径");
            Program.Check(request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == configuration.ApiKey, "请求使用配置中的 Bearer 鉴权");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Program.Check(body.RootElement.GetProperty("model").GetString() == "test-model" &&
                body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString() == "Reply only with OK." &&
                !body.RootElement.GetProperty("stream").GetBoolean(), "请求指定模型且只发送一次简短文本检查");
            return Reply(200, ValidResponse);
        }));
        var success = await new AiConnectionService(successClient).CheckAsync(configuration);
        Program.Check(success.Success && success.Message.Contains("HTTP 200"), "只有有效模型文本才返回连接成功");

        foreach (var status in new[] { 400, 401, 403, 404, 408, 422, 429, 500, 502, 503, 504, 418, 302 })
        {
            var requests = 0;
            using var errorClient = new HttpClient(new Handler((_, _) =>
            {
                requests++;
                return Task.FromResult(Reply(status, JsonSerializer.Serialize(new { error = new { message = "API key: " + configuration.ApiKey + " Authorization: Bearer another-secret" } })));
            }));
            var error = await new AiConnectionService(errorClient).CheckAsync(configuration);
            Program.Check(!error.Success && error.Message.Contains("HTTP " + status) &&
                !error.Message.Contains(configuration.ApiKey) && !error.Message.Contains("another-secret") && requests == 1,
                "服务错误保留实际状态码、脱敏密钥且不自动重复调用");
        }
        foreach (var invalid in new[]
        {
            "{}", "[]", "<html>ok</html>", """{"choices":[]}""",
            """{"choices":[{"message":{"role":"assistant","content":""}}]}""",
            """{"choices":[{"message":{"role":"assistant","content":null,"reasoning_content":"thinking"}}]}""",
            """{"choices":[{"message":{"role":"user","content":"OK"}}]}"""
        })
        {
            using var invalidClient = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(200, invalid))));
            var result = await new AiConnectionService(invalidClient).CheckAsync(configuration);
            Program.Check(!result.Success && result.Message.Contains("HTTP 200") && result.Message.Contains("LOCAL_RESPONSE_INVALID"), "HTTP 200 但无有效模型回复不能报成功");
        }
        using var networkClient = new HttpClient(new Handler((_, _) => throw new HttpRequestException("unsafe request details")));
        var network = await new AiConnectionService(networkClient).CheckAsync(configuration);
        Program.Check(!network.Success && network.Message.Contains("LOCAL_NETWORK_ERROR") && !network.Message.Contains("HTTP ") && !network.Message.Contains("unsafe"), "网络错误使用本地代码且不泄露异常详情");

        using var timeoutClient = new HttpClient(new Handler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Reply(200, ValidResponse);
        })) { Timeout = TimeSpan.FromMilliseconds(50) };
        var timeout = await new AiConnectionService(timeoutClient).CheckAsync(configuration);
        Program.Check(!timeout.Success && timeout.Message.Contains("LOCAL_TIMEOUT") && !timeout.Message.Contains("HTTP "), "客户端超时不伪造 HTTP 状态码");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await new AiConnectionService(successClient).CheckAsync(configuration, cancellation.Token);
            Program.Check(false, "主动取消必须传播取消状态");
        }
        catch (OperationCanceledException) { Program.Check(true, "主动取消不伪装超时"); }

        using var activeCancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var waitingClient = new HttpClient(new Handler(async (_, token) =>
        {
            started.SetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Reply(200, ValidResponse);
        }));
        var pending = new AiConnectionService(waitingClient).CheckAsync(configuration, activeCancellation.Token);
        await started.Task;
        activeCancellation.Cancel();
        try
        {
            await pending;
            Program.Check(false, "进行中的请求必须可取消");
        }
        catch (OperationCanceledException) { Program.Check(true, "进行中的模型请求取消后不会误报失败或成功"); }
        var invalidConfiguration = configuration.Copy(); invalidConfiguration.Endpoint = "https://user:password@example.test/v1?api_key=secret";
        var invalidResult = await new AiConnectionService(successClient).CheckAsync(invalidConfiguration);
        Program.Check(!invalidResult.Success && invalidResult.Message.Contains("LOCAL_CONFIG_INVALID") && !invalidResult.Message.Contains("password"), "不合法地址在联网前拒绝且不泄露地址中的凭证");
    }

    private const string ValidResponse = """{"choices":[{"message":{"role":"assistant","content":"OK"}}]}""";
    private static HttpResponseMessage Reply(int status, string body) => new((HttpStatusCode)status)
    { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
}
