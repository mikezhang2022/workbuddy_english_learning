using FactoryReport.Client.Configuration;
using FactoryReport.Client.Services.Api;
using FactoryReport.Client.Services.Auth;
using FactoryReport.Client.Services.Connectivity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace FactoryReport.Client;

/// <summary>
/// Client DI 注册，供 Program 与回归测试共用，避免测试与真实注册脱节。
/// </summary>
public static class ClientServiceCollectionExtensions
{
    public static IServiceCollection AddClientServices(
        this IServiceCollection services,
        ClientApiOptions apiOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(apiOptions);

        if (!Uri.TryCreate(apiOptions.ApiBaseUrl, UriKind.Absolute, out var apiBaseUri))
        {
            throw new InvalidOperationException("FactoryReportClient:ApiBaseUrl 配置无效。");
        }

        services.AddScoped<CredentialsHttpHandler>();
        services.AddHttpClient(ApiConstants.HttpClientName, client =>
            {
                client.BaseAddress = apiBaseUri;
            })
            .AddHttpMessageHandler<CredentialsHttpHandler>();

        services.AddScoped<AuthApiClient>();
        services.AddScoped<ReportsApiClient>();
        services.AddScoped<ImportApiClient>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<CookieAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<CookieAuthenticationStateProvider>());
        services.AddAuthorizationCore();
        services.AddMudServices();
        services.AddScoped<IOnlineStatusService, OnlineStatusService>();

        return services;
    }
}
