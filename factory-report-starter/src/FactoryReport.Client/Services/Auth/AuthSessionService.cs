using FactoryReport.Client.Services.Api;
using Microsoft.AspNetCore.Components.Authorization;

namespace FactoryReport.Client.Services.Auth;

public interface IAuthSessionService
{
    AuthMeDto? CurrentUser { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task LoginAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    event Action? SessionChanged;
}

/// <summary>
/// 进程内会话状态；CSRF 仅存内存，不写入 Web Storage。
/// </summary>
public sealed class AuthSessionService(
    AuthApiClient authApiClient,
    AuthenticationStateProvider authenticationStateProvider) : IAuthSessionService
{
    private string? _csrfToken;

    public AuthMeDto? CurrentUser { get; private set; }
    public bool IsInitialized { get; private set; }

    public event Action? SessionChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        try
        {
            CurrentUser = await authApiClient.GetMeAsync(cancellationToken).ConfigureAwait(false);
            if (CurrentUser is not null)
            {
                _csrfToken = await authApiClient.RefreshCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (ApiRequestException)
        {
            CurrentUser = null;
            _csrfToken = null;
        }

        IsInitialized = true;
        NotifyChanged();
    }

    public async Task LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var (user, csrf) = await authApiClient.LoginAsync(userName, password, cancellationToken).ConfigureAwait(false);
        CurrentUser = user;
        _csrfToken = csrf ?? await authApiClient.RefreshCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
        IsInitialized = true;
        NotifyChanged();
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentUser is not null)
        {
            var csrf = _csrfToken
                       ?? await authApiClient.RefreshCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(csrf))
            {
                await authApiClient.LogoutAsync(csrf, cancellationToken).ConfigureAwait(false);
            }
        }

        CurrentUser = null;
        _csrfToken = null;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        SessionChanged?.Invoke();
        if (authenticationStateProvider is CookieAuthenticationStateProvider cookieProvider)
        {
            cookieProvider.NotifyAuthenticationStateChanged();
        }
    }
}
