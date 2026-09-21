using FactoryReport.Client.Services.Api;

namespace FactoryReport.Client.Services.Auth;

public interface IAuthSessionService
{
    AuthMeDto? CurrentUser { get; }
    bool IsInitialized { get; }
    /// <summary>内存中的 CSRF 令牌；未登录时为 null。</summary>
    string? CsrfToken { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task LoginAsync(string userName, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    /// <summary>确保已有 CSRF；必要时刷新。</summary>
    Task<string> EnsureCsrfTokenAsync(CancellationToken cancellationToken = default);
    /// <summary>本地清除会话（如 API 返回 401）；不调用 logout 接口。</summary>
    void ClearLocalSession();
    event Action? SessionChanged;
}

/// <summary>
/// 进程内会话状态；CSRF 仅存内存，不写入 Web Storage。
/// 通过 <see cref="SessionChanged"/> 单向通知认证状态提供者，避免与 AuthenticationStateProvider 循环依赖。
/// </summary>
public sealed class AuthSessionService(AuthApiClient authApiClient) : IAuthSessionService
{
    private string? _csrfToken;

    public AuthMeDto? CurrentUser { get; private set; }
    public bool IsInitialized { get; private set; }
    public string? CsrfToken => _csrfToken;

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
            if (string.IsNullOrWhiteSpace(csrf))
            {
                throw new ApiRequestException(new ApiProblemDetails
                {
                    Detail = "无法获取 CSRF 令牌，退出登录失败。请刷新页面后重试。",
                    Status = 400
                });
            }

            await authApiClient.LogoutAsync(csrf, cancellationToken).ConfigureAwait(false);
        }

        ClearLocalSession();
    }

    public async Task<string> EnsureCsrfTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_csrfToken))
        {
            return _csrfToken;
        }

        _csrfToken = await authApiClient.RefreshCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(_csrfToken))
        {
            throw new ApiRequestException(new ApiProblemDetails
            {
                Detail = "无法获取 CSRF 令牌。请重新登录后重试。",
                Status = 401
            });
        }

        return _csrfToken;
    }

    public void ClearLocalSession()
    {
        CurrentUser = null;
        _csrfToken = null;
        NotifyChanged();
    }

    private void NotifyChanged() => SessionChanged?.Invoke();
}
