using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace FactoryReport.Client.Services.Api;

/// <summary>
/// 跨源请求携带 Cookie 会话（浏览器 fetch credentials: include）。
/// </summary>
public sealed class CredentialsHttpHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        request.SetBrowserRequestMode(BrowserRequestMode.Cors);
        return base.SendAsync(request, cancellationToken);
    }
}
