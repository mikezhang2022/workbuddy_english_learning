using System.Net.Http.Json;
using FactoryReport.Api.Endpoints;
using FactoryReport.Infrastructure.Fake;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FactoryReport.IntegrationTests;

internal static class AuthenticatedClientFactory
{
    public static HttpClient CreateCookieClient(ApiWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    public static async Task<HttpClient> CreateLoggedInClientAsync(
        ApiWebApplicationFactory factory,
        string userName,
        string password)
    {
        var client = CreateCookieClient(factory);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });
        login.EnsureSuccessStatusCode();
        return client;
    }

    public static Task<HttpClient> CreateSystemAdminClientAsync(ApiWebApplicationFactory factory)
        => CreateLoggedInClientAsync(
            factory,
            "sysadmin",
            FakeLocalAccountStore.DevPassword_SystemAdmin);

    public static Task<HttpClient> CreateFactoryAdminClientAsync(ApiWebApplicationFactory factory)
        => CreateLoggedInClientAsync(
            factory,
            "factoryadmin",
            FakeLocalAccountStore.DevPassword_FactoryAdmin);

    public static Task<HttpClient> CreateProductionManagerClientAsync(ApiWebApplicationFactory factory)
        => CreateLoggedInClientAsync(
            factory,
            "prodmanager",
            FakeLocalAccountStore.DevPassword_ProductionManager);

    public static Task<HttpClient> CreateQualityUserClientAsync(ApiWebApplicationFactory factory)
        => CreateLoggedInClientAsync(
            factory,
            "qualityuser",
            FakeLocalAccountStore.DevPassword_QualityUser);

    public static Task<HttpClient> CreateViewerClientAsync(ApiWebApplicationFactory factory)
        => CreateLoggedInClientAsync(
            factory,
            "viewer",
            FakeLocalAccountStore.DevPassword_Viewer);
}
