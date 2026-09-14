using FactoryReport.Application.Security;
using FactoryReport.Application.Security.DataScope;
using FactoryReport.Domain.Security;
using FactoryReport.Infrastructure.Security;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// 进程内 Fake 本地账号存储（含确定性组织数据范围）。
/// <para>
/// 『仅用于开发测试 / 自动化，不得当作正式生产账号』。
/// 数据范围写在服务端账号配置中，不得依赖客户端 factoryId / Header / 角色 Claim。
/// Production 环境若仍启用本存储，启动必须失败。
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

    public const string UserId_SystemAdmin = "u-sys-admin";
    public const string UserId_FactoryAdmin = "u-factory-admin";
    public const string UserId_ProductionManager = "u-prod-mgr";
    public const string UserId_QualityUser = "u-quality";
    public const string UserId_Viewer = "u-viewer";

    private readonly IReadOnlyDictionary<string, LocalAccount> _accountsByUserName;
    private readonly IReadOnlyDictionary<string, LocalAccount> _accountsByUserId;

    public FakeLocalAccountStore(IPasswordHasher passwordHasher)
    {
        ArgumentNullException.ThrowIfNull(passwordHasher);
        var accounts = CreateSeed(passwordHasher).ToArray();
        _accountsByUserName = accounts.ToDictionary(a => a.UserName, StringComparer.OrdinalIgnoreCase);
        _accountsByUserId = accounts.ToDictionary(a => a.UserId, StringComparer.Ordinal);
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

    public Task<LocalAccount?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<LocalAccount?>(null);
        }

        return Task.FromResult(
            _accountsByUserId.TryGetValue(userId.Trim(), out var account) ? account : null);
    }

    private static IEnumerable<LocalAccount> CreateSeed(IPasswordHasher passwordHasher)
    {
        // 全部 IsTestOnlyAccount=true：明确标注非生产账号。
        // 数据范围确定性（对齐 DeterministicFakeFixture 组织 Id）：
        // - SystemAdmin：全局
        // - FactoryAdmin：仅工厂 1（工厂级）
        // - ProductionManager：工厂 1 / 车间 A（车间级，不继承车间 B）
        // - QualityUser：工厂 1 / 车间 B / 产线 B1（产线级）
        // - Viewer：仅工厂 2（工厂级）
        yield return Create(
            passwordHasher,
            UserId_SystemAdmin,
            "sysadmin",
            "系统管理员(测试)",
            DevPassword_SystemAdmin,
            UserDataScope.Global,
            AppRoles.SystemAdmin);

        yield return Create(
            passwordHasher,
            UserId_FactoryAdmin,
            "factoryadmin",
            "工厂管理员(测试)",
            DevPassword_FactoryAdmin,
            UserDataScope.FromGrants(new DataScopeGrant(DeterministicFakeFixture.FactoryDemo1Id)),
            AppRoles.FactoryAdmin);

        yield return Create(
            passwordHasher,
            UserId_ProductionManager,
            "prodmanager",
            "生产主管(测试)",
            DevPassword_ProductionManager,
            UserDataScope.FromGrants(
                new DataScopeGrant(
                    DeterministicFakeFixture.FactoryDemo1Id,
                    DeterministicFakeFixture.WorkshopAId)),
            AppRoles.ProductionManager);

        yield return Create(
            passwordHasher,
            UserId_QualityUser,
            "qualityuser",
            "质量用户(测试)",
            DevPassword_QualityUser,
            UserDataScope.FromGrants(
                new DataScopeGrant(
                    DeterministicFakeFixture.FactoryDemo1Id,
                    DeterministicFakeFixture.WorkshopBId,
                    DeterministicFakeFixture.LineB1Id)),
            AppRoles.QualityUser);

        yield return Create(
            passwordHasher,
            UserId_Viewer,
            "viewer",
            "只读查看(测试)",
            DevPassword_Viewer,
            UserDataScope.FromGrants(new DataScopeGrant(DeterministicFakeFixture.FactoryDemo2Id)),
            AppRoles.Viewer);
    }

    private static LocalAccount Create(
        IPasswordHasher passwordHasher,
        string userId,
        string userName,
        string displayName,
        string password,
        IDataScope dataScope,
        params string[] roles)
        => new()
        {
            UserId = userId,
            UserName = userName,
            DisplayName = displayName,
            PasswordHash = passwordHasher.HashPassword(password),
            Roles = roles,
            DataScope = dataScope,
            IsTestOnlyAccount = true
        };
}
