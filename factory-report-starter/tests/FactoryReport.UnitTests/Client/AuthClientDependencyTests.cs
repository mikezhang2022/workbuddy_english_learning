using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryReport.Client;
using FactoryReport.Client.Configuration;
using FactoryReport.Client.Services.Api;
using FactoryReport.Client.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FactoryReport.UnitTests.Client;

public class AuthClientDependencyTests
{
    [Fact]
    public void AddClientServices_ResolvesAuthSessionAndAuthenticationStateProvider_WithoutCircularDependency()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, NoopJsRuntime>();
        services.AddClientServices(new ClientApiOptions { ApiBaseUrl = "http://localhost:5161/" });

        // 不用 ValidateOnBuild：MudBlazor 还依赖 NavigationManager 等宿主服务；
        // 工厂注册的循环依赖只能在实际解析时暴露（本测试目标）。
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = false
        });

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        var session = sp.GetRequiredService<IAuthSessionService>();
        var authStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
        var cookieProvider = sp.GetRequiredService<CookieAuthenticationStateProvider>();

        Assert.Same(cookieProvider, authStateProvider);
        Assert.IsType<AuthSessionService>(session);
        Assert.IsType<CookieAuthenticationStateProvider>(authStateProvider);

        // 再从另一侧解析，确认双向均可构建且无环。
        Assert.Same(session, sp.GetRequiredService<IAuthSessionService>());
        Assert.Same(cookieProvider, sp.GetRequiredService<CookieAuthenticationStateProvider>());
    }
}

public class AuthSessionAuthenticationStateTests
{
    [Fact]
    public async Task Login_RaisesSessionChanged_AndAuthenticationStateBecomesAuthenticated()
    {
        await using var harness = AuthSessionTestHarness.Create();

        AuthenticationState? observed = null;
        var signaled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.AuthProvider.AuthenticationStateChanged += async task =>
        {
            observed = await task.ConfigureAwait(false);
            signaled.TrySetResult();
        };

        var sessionChanged = 0;
        harness.Session.SessionChanged += () => sessionChanged++;

        await harness.Session.LoginAsync("sysadmin", "Dev-Only-SystemAdmin-Passw0rd!");

        await signaled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(sessionChanged >= 1);
        Assert.NotNull(observed);
        Assert.True(observed!.User.Identity?.IsAuthenticated);
        Assert.Equal("sysadmin", observed.User.Identity?.Name);

        var state = await harness.AuthProvider.GetAuthenticationStateAsync();
        Assert.True(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task ClearLocalSession_RaisesSessionChanged_AndAuthenticationStateBecomesAnonymous()
    {
        await using var harness = AuthSessionTestHarness.Create();
        await harness.Session.LoginAsync("sysadmin", "Dev-Only-SystemAdmin-Passw0rd!");

        AuthenticationState? observed = null;
        var signaled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.AuthProvider.AuthenticationStateChanged += async task =>
        {
            observed = await task.ConfigureAwait(false);
            signaled.TrySetResult();
        };

        harness.Session.ClearLocalSession();

        await signaled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(observed);
        Assert.False(observed!.User.Identity?.IsAuthenticated);

        var state = await harness.AuthProvider.GetAuthenticationStateAsync();
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Logout_ClearsSession_AndAuthenticationStateBecomesAnonymous()
    {
        await using var harness = AuthSessionTestHarness.Create();
        await harness.Session.LoginAsync("sysadmin", "Dev-Only-SystemAdmin-Passw0rd!");

        AuthenticationState? observed = null;
        var signaled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.AuthProvider.AuthenticationStateChanged += async task =>
        {
            observed = await task.ConfigureAwait(false);
            signaled.TrySetResult();
        };

        await harness.Session.LogoutAsync();

        await signaled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(observed);
        Assert.False(observed!.User.Identity?.IsAuthenticated);
        Assert.Null(harness.Session.CurrentUser);
    }
}

internal sealed class AuthSessionTestHarness : IAsyncDisposable
{
    private AuthSessionTestHarness(
        AuthSessionService session,
        CookieAuthenticationStateProvider authProvider,
        StubAuthHttpHandler handler)
    {
        Session = session;
        AuthProvider = authProvider;
        _handler = handler;
    }

    public AuthSessionService Session { get; }
    public CookieAuthenticationStateProvider AuthProvider { get; }
    private readonly StubAuthHttpHandler _handler;

    public static AuthSessionTestHarness Create()
    {
        var handler = new StubAuthHttpHandler();
        var factory = new SingleClientHttpClientFactory(handler);
        var authApi = new AuthApiClient(factory);
        var session = new AuthSessionService(authApi);
        var authProvider = new CookieAuthenticationStateProvider(session);
        return new AuthSessionTestHarness(session, authProvider, handler);
    }

    public ValueTask DisposeAsync()
    {
        AuthProvider.Dispose();
        _handler.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class SingleClientHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpClient _client = new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost:5161/")
        };

        public HttpClient CreateClient(string name) => _client;
    }
}

internal sealed class StubAuthHttpHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (path.EndsWith("/auth/login", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Post)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new AuthMeDto
                    {
                        UserId = "u-sysadmin",
                        UserName = "sysadmin",
                        DisplayName = "System Admin",
                        Roles = ["SystemAdmin"],
                        IsAuthenticated = true
                    },
                    options: JsonOptions)
            };
            response.Headers.TryAddWithoutValidation(ApiConstants.AntiforgeryHeaderName, "csrf-test-token");
            return Task.FromResult(response);
        }

        if (path.EndsWith("/auth/logout", StringComparison.OrdinalIgnoreCase)
            && request.Method == HttpMethod.Post)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }

        if (path.EndsWith("/auth/me", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }

        if (path.EndsWith("/auth/csrf", StringComparison.OrdinalIgnoreCase))
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            response.Headers.TryAddWithoutValidation(ApiConstants.AntiforgeryHeaderName, "csrf-test-token");
            return Task.FromResult(response);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

internal sealed class NoopJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => throw new NotSupportedException();

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args)
        => throw new NotSupportedException();
}
