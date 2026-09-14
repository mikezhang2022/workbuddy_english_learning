using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FactoryReport.Api.Endpoints;
using FactoryReport.Api.Security;
using FactoryReport.Domain.Security;
using FactoryReport.Infrastructure.Fake;
using FactoryReport.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using FactoryReport.Application.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FactoryReport.IntegrationTests;

public class AuthApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public AuthApiTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsAuthCookie_AndMeSucceeds()
    {
        var client = CreateCookieClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = "viewer",
            Password = FakeLocalAccountStore.DevPassword_Viewer
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(login.Headers.Contains(FactoryReportAuthDefaults.AntiforgeryHeaderName));

        var meBody = await login.Content.ReadFromJsonAsync<AuthMeResponse>();
        Assert.NotNull(meBody);
        Assert.Equal("viewer", meBody!.UserName);
        Assert.Contains(AppRoles.Viewer, meBody.Roles);
        Assert.True(meBody.IsAuthenticated);

        Assert.Contains(
            login.Headers.TryGetValues("Set-Cookie", out var setCookies) ? setCookies : [],
            c => c.Contains(FactoryReportAuthDefaults.AuthCookieName, StringComparison.Ordinal));

        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var current = await me.Content.ReadFromJsonAsync<AuthMeResponse>();
        Assert.NotNull(current);
        Assert.Equal("u-viewer", current!.UserId);
        Assert.Equal(new[] { AppRoles.Viewer }, current.Roles.ToArray());
    }

    [Theory]
    [InlineData("viewer", "wrong-password")]
    [InlineData("does-not-exist", "any-password")]
    [InlineData("viewer", FakeLocalAccountStore.DevPassword_SystemAdmin)]
    public async Task Login_WithInvalidCredentials_ReturnsSameUnauthorized(string userName, string password)
    {
        var client = CreateCookieClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(401, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "Invalid username or password.",
            doc.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Me_WhenAnonymous_ReturnsUnauthorized()
    {
        var client = CreateCookieClient();
        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Csrf_WhenAuthenticated_ReturnsTokenHeader()
    {
        var client = CreateCookieClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = "viewer",
            Password = FakeLocalAccountStore.DevPassword_Viewer
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.NoContent, csrfResponse.StatusCode);
        Assert.True(csrfResponse.Headers.Contains(FactoryReportAuthDefaults.AntiforgeryHeaderName));
    }

    [Fact]
    public async Task Csrf_WhenAnonymous_ReturnsUnauthorized()
    {
        var client = CreateCookieClient();
        var response = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_InvalidatesSession()
    {
        var client = CreateCookieClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = "sysadmin",
            Password = FakeLocalAccountStore.DevPassword_SystemAdmin
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var csrf = login.Headers.GetValues(FactoryReportAuthDefaults.AntiforgeryHeaderName).Single();

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.TryAddWithoutValidation(
            FactoryReportAuthDefaults.AntiforgeryHeaderName,
            csrf);

        var logout = await client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Login_GeneratesRoleClaims_MatchingPolicies()
    {
        var client = CreateCookieClient();

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = "prodmanager",
            Password = FakeLocalAccountStore.DevPassword_ProductionManager
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await client.GetFromJsonAsync<AuthMeResponse>("/api/v1/auth/me");
        Assert.NotNull(me);
        Assert.Equal(new[] { AppRoles.ProductionManager }, me!.Roles.ToArray());

        // 策略已在 DI 注册；通过 Authorization 服务解析验证角色策略可用。
        using var scope = _factory.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        var accessor = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();

        // 使用登录后 Cookie 再发一次请求以建立 HttpContext 用户较复杂；改为直接用 ClaimsPrincipal 测策略。
        var principal = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                FactoryReport.Application.Security.LocalAccountAuthenticationService.BuildClaims(
                    new FactoryReport.Application.Security.CurrentUser
                    {
                        UserId = me.UserId,
                        UserName = me.UserName,
                        DisplayName = me.DisplayName,
                        Roles = me.Roles
                    }),
                "Test"));

        var canProd = await auth.AuthorizeAsync(principal, resource: null, AuthorizationPolicies.CanViewProductionReports);
        var canQuality = await auth.AuthorizeAsync(principal, resource: null, AuthorizationPolicies.CanViewQualityReports);
        var requireProd = await auth.AuthorizeAsync(principal, resource: null, AuthorizationPolicies.RequireProductionManager);
        var requireAdmin = await auth.AuthorizeAsync(principal, resource: null, AuthorizationPolicies.RequireSystemAdmin);

        Assert.True(canProd.Succeeded);
        Assert.False(canQuality.Succeeded);
        Assert.True(requireProd.Succeeded);
        Assert.False(requireAdmin.Succeeded);
        _ = accessor;
    }

    [Fact]
    public async Task Login_DoesNotLogPassword_OrAuthCookie()
    {
        var sink = new CollectingLoggerProvider();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(sink);
                });
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        const string password = FakeLocalAccountStore.DevPassword_Viewer;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = "viewer",
            Password = password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var joined = string.Join('\n', sink.Messages);
        Assert.DoesNotContain(password, joined, StringComparison.Ordinal);
        Assert.DoesNotContain("Cookie:", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            FactoryReportAuthDefaults.AuthCookieName + "=",
            joined,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization:", joined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistingReportApi_RequiresAuthentication()
    {
        var client = CreateCookieClient();
        var response = await client.GetAsync(
            "/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateCookieClient()
        => _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
}

public class FakeAccountStoreProductionGuardTests
{
    [Fact]
    public void EnsureNotFakeInProduction_Throws_WhenFakeEnabled()
    {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var options = new FactoryReportOptions
        {
            Authentication = new AuthenticationOptions { AccountStore = "Fake" }
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => FakeAccountStoreProductionGuard.EnsureNotFakeInProduction(env, options));
        Assert.Contains("must not run in Production", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureNotFakeInProduction_Allows_DevelopmentFake()
    {
        var env = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var options = new FactoryReportOptions
        {
            Authentication = new AuthenticationOptions { AccountStore = "Fake" }
        };

        FakeAccountStoreProductionGuard.EnsureNotFakeInProduction(env, options);
    }

    [Fact]
    public void ProductionHost_WithFakeAccountStore_FailsToStart()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["FactoryReport:DataMode"] = "Fake",
                        ["FactoryReport:Authentication:AccountStore"] = "Fake",
                        ["FactoryReport:Worker:HeartbeatIntervalSeconds"] = "30"
                    });
                });
            });

        var ex = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        Assert.Contains(
            "must not run in Production",
            GetFullMessage(ex),
            StringComparison.Ordinal);
    }

    private static string GetFullMessage(Exception ex)
    {
        var sb = new StringBuilder(ex.Message);
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            sb.Append(' ').Append(inner.Message);
        }

        return sb.ToString();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "FactoryReport.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _messages = new();
    public IReadOnlyList<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, _messages);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(string category, List<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Add($"{category}|{formatter(state, exception)}");
        }
    }
}
