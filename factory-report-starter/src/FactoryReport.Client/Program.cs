using FactoryReport.Client;
using FactoryReport.Client.Configuration;
using FactoryReport.Client.Services.Api;
using FactoryReport.Client.Services.Auth;
using FactoryReport.Client.Services.Connectivity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiOptions = builder.Configuration
    .GetSection(ClientApiOptions.SectionName)
    .Get<ClientApiOptions>() ?? new ClientApiOptions();

if (!Uri.TryCreate(apiOptions.ApiBaseUrl, UriKind.Absolute, out var apiBaseUri))
{
    throw new InvalidOperationException("FactoryReportClient:ApiBaseUrl 配置无效。");
}

builder.Services.AddScoped<CredentialsHttpHandler>();
builder.Services.AddHttpClient(ApiConstants.HttpClientName, client =>
    {
        client.BaseAddress = apiBaseUri;
    })
    .AddHttpMessageHandler<CredentialsHttpHandler>();

builder.Services.AddScoped<AuthApiClient>();
builder.Services.AddScoped<ReportsApiClient>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
builder.Services.AddScoped<CookieAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddMudServices();
builder.Services.AddScoped<IOnlineStatusService, OnlineStatusService>();

await builder.Build().RunAsync();
