using FactoryReport.Application.Security;
using FactoryReport.Domain.Security;
using FactoryReport.Infrastructure.Security;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// 进程内 Fake 本地账号存储。
/// <para>
/// 『仅用于开发测试 / 自动化，不得当作正式生产账号』。
/// 不读取外部文件、网络或数据库。Production 环境若仍启用本存储，启动必须失败。
/// </para>
/// </summary>
public sealed class FakeLocalAccountStore : ILocalAccountStore
{
    public const string StoreKindName = "Fake";

    /// <summary>
    /// 测试口令仅存在于测试代码与本文档注释用途的常量中，禁止写入 README 作为生产默认管理员密码。
    /// </summary>
    public const string DevPassword_SystemAdmin = "Dev-Only-SystemAdmin-Passw0rd!";
    public const string DevPassword_FactoryAdmin = "Dev-Only-FactoryAdmin-Passw0rd!";
    public const string DevPassword_ProductionManager = "Dev-Only-ProdMgr-Passw0rd!";
    public const string DevPassword_QualityUser = "Dev-Only-Quality-Passw0rd!";
    public const string DevPassword_Viewer = "Dev-Only-Viewer-Passw0rd!";

    private readonly IReadOnlyDictionary<string, LocalAccount> _accountsByUserName;

    public FakeLocalAccountStore(IPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        _accountsByUserName = CreateSeed(passwordHasher)
            .ToDictionary(a => a.UserName, StringComparer.OrdinalIgnoreCase);
    }

    public string StoreKind => StoreKindName;

    public bool IsFake => true;

    public Task<LocalAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Task.FromResult<LocalAccount?>(null);
        }

        return Task.FromResult(
            _accountsByUserName.TryGetValue(userName.Trim(), out var account) ? account : null);
    }

    private static IEnumerable<LocalAccount> CreateSeed(IPasswordHasher passwordHasher)
    {
        // 全部 IsTestOnlyAccount=true：明确标注非生产账号。
        yield return Create(passwordHasher, "u-sys-admin", "sysadmin", "系统管理员(测试)", DevPassword_SystemAdmin, AppRoles.SystemAdmin);
        yield return Create(passwordHasher, "u-factory-admin", "factoryadmin", "工厂管理员(测试)", DevPassword_FactoryAdmin, AppRoles.FactoryAdmin);
        yield return Create(passwordHasher, "u-prod-mgr", "prodmanager", "生产主管(测试)", DevPassword_ProductionManager, AppRoles.ProductionManager);
        yield return Create(passwordHasher, "u-quality", "qualityuser", "质量用户(测试)", DevPassword_QualityUser, AppRoles.QualityUser);
        yield return Create(passwordHasher, "u-viewer", "viewer", "只读查看(测试)", DevPassword_Viewer, AppRoles.Viewer);
    }

    private static LocalAccount Create(
        IPasswordHasher passwordHasher,
        string userId,
        string userName,
        string displayName,
        string password,
        params string[] roles)
        => new()
        {
            UserId = userId,
            UserName = userName,
            DisplayName = displayName,
            PasswordHash = passwordHasher.HashPassword(password),
            Roles = roles,
            IsTestOnlyAccount = true
        };
}
