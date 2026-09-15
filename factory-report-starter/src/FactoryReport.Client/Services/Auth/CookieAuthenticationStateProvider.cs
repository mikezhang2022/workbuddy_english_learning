using System.Security.Claims;
using FactoryReport.Client.Services.Api;
using Microsoft.AspNetCore.Components.Authorization;

namespace FactoryReport.Client.Services.Auth;

/// <summary>
/// 单向依赖 <see cref="IAuthSessionService"/>，订阅 SessionChanged 刷新 Blazor 认证状态。
/// </summary>
public sealed class CookieAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IAuthSessionService _sessionService;
    private bool _disposed;

    public CookieAuthenticationStateProvider(IAuthSessionService sessionService)
    {
        _sessionService = sessionService;
        _sessionService.SessionChanged += OnSessionChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = _sessionService.CurrentUser;
        if (user is null || !user.IsAuthenticated)
        {
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName)
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, authenticationType: "cookie");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    private void OnSessionChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _sessionService.SessionChanged -= OnSessionChanged;
        _disposed = true;
    }
}
