using FactoryReport.Application.Security;
using FactoryReport.Application.Security.DataScope;

namespace FactoryReport.Infrastructure.Security;

/// <summary>
/// 按服务端账号存储解析数据范围（Fake / 未来 Oracle）。
/// 忽略 Cookie 中的角色 Claim，防止伪造 Claim 扩大范围。
/// </summary>
public sealed class LocalAccountUserScopeResolver(ILocalAccountStore accountStore) : IUserScopeResolver
{
    public async Task<IDataScope> ResolveAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return UserDataScope.Empty;
        }

        var account = await accountStore
            .FindByUserIdAsync(userId.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return account?.DataScope ?? UserDataScope.Empty;
    }
}
