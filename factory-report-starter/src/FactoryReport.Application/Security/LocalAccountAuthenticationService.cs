using System.Security.Claims;
using FactoryReport.Domain.Security;

namespace FactoryReport.Application.Security;

/// <summary>
/// 本地账号登录验证（不签发 Cookie；Cookie 由 API 宿主完成）。
/// </summary>
public interface ILocalAccountAuthenticationService
{
    /// <summary>
    /// 验证用户名密码。失败时不区分用户名不存在与密码错误。
    /// </summary>
    Task<LocalAccountAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default);
}

public sealed class LocalAccountAuthenticationResult
{
    public bool Succeeded { get; init; }
    public CurrentUser? User { get; init; }
    public IReadOnlyList<Claim> Claims { get; init; } = [];

    public static LocalAccountAuthenticationResult Fail()
        => new() { Succeeded = false };

    public static LocalAccountAuthenticationResult Success(CurrentUser user, IReadOnlyList<Claim> claims)
        => new() { Succeeded = true, User = user, Claims = claims };
}

/// <summary>
/// 本地账号凭证校验。成功后由 API 签发 Cookie；失败对外统一不可枚举。
/// </summary>
public sealed class LocalAccountAuthenticationService(
    ILocalAccountStore accountStore,
    IPasswordHasher passwordHasher) : ILocalAccountAuthenticationService
{
    public const string FailureMessage = "Invalid username or password.";

    public async Task<LocalAccountAuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        // 故意统一失败路径，避免用户名枚举。
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            return LocalAccountAuthenticationResult.Fail();
        }

        var normalized = userName.Trim();
        var account = await accountStore.FindByUserNameAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return LocalAccountAuthenticationResult.Fail();
        }

        if (!passwordHasher.VerifyHashedPassword(account.PasswordHash, password))
        {
            return LocalAccountAuthenticationResult.Fail();
        }

        var roles = account.Roles
            .Where(AppRoles.IsKnown)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var user = new CurrentUser
        {
            UserId = account.UserId,
            UserName = account.UserName,
            DisplayName = account.DisplayName,
            Roles = roles,
            IsAuthenticated = true
        };

        return LocalAccountAuthenticationResult.Success(user, BuildClaims(user));
    }

    public static IReadOnlyList<Claim> BuildClaims(CurrentUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName)
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return claims;
    }
}
