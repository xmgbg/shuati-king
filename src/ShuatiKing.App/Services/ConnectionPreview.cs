namespace ShuatiKing.Services;

// 前端连接状态预览，不发送网络请求。以下是后续真实连接检测应沿用的中文说明表：
// 200：请求成功，还须校验有效模型响应；无有效结果使用 LOCAL_RESPONSE_INVALID。
// 400 / 422：参数不符合服务要求，检查配置及服务说明。
// 401：鉴权失败，检查 API 密钥。403：访问被拒绝，检查账号及模型权限。
// 404：资源不存在，检查地址、接口路径及模型。408：服务端超时，稍后重试。
// 429：请求受限，按服务返回说明处理后重试。
// 500：服务内部错误。502：上游响应异常。503：服务不可用。504：上游超时；可稍后重试。
// LOCAL_TIMEOUT：客户端超时，未取得有效响应，检查网络后重试。
// LOCAL_NETWORK_ERROR：网络、DNS 或 TLS 连接失败，检查网络和服务地址。
// LOCAL_RESPONSE_INVALID：响应无有效模型结果，检查接口兼容性，并保留实际 HTTP 状态码。
// 真实服务接入后必须原样显示未列出的 HTTP 状态码，不能用本地错误码伪装 HTTP 状态码。
public static class ConnectionPreview
{
    public static string Message(string code) => code switch
    {
        "200" => "演示 · 连接成功\nHTTP 200 · 已收到有效模型响应。此结果仅用于界面预览。",
        "401" => "演示 · 连接失败\nHTTP 401 · 鉴权失败，请检查 API 密钥后重试。",
        "404" => "演示 · 连接失败\nHTTP 404 · 资源不存在，请检查服务地址、接口路径和模型名称。",
        "429" => "演示 · 连接失败\nHTTP 429 · 请求受限，请检查服务额度或稍后重试。",
        "500" => "演示 · 连接失败\nHTTP 500 · 服务内部错误，请稍后重试。",
        "LOCAL_TIMEOUT" => "演示 · 连接失败\nLOCAL_TIMEOUT（本地错误码）· 等待超时，未取得响应，请检查网络后重试。",
        "LOCAL_NETWORK_ERROR" => "演示 · 连接失败\nLOCAL_NETWORK_ERROR（本地错误码）· 无法建立连接，请检查网络及服务地址。",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}
