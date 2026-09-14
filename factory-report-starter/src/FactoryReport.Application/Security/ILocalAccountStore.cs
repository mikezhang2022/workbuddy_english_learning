namespace FactoryReport.Application.Security;

/// <summary>
/// 本地账号读取与验证抽象。
/// Fake 实现：进程内内存（开发/测试）。
/// 未来 Oracle 实现：见 Infrastructure/Persistence/Oracle 占位与 docs/authentication.md。
/// </summary>
public interface ILocalAccountStore
{
    /// <summary>
    /// 账号存储实现标识：Fake 或 Oracle。
    /// </summary>
    string StoreKind { get; }

    /// <summary>
    /// 是否为仅开发/测试用的 Fake 存储。Production 不得启用。
    /// </summary>
    bool IsFake { get; }

    /// <summary>
    /// 按用户名查找账号；不存在返回 null。查找本身不区分「用户不存在 / 密码错误」对外语义。
    /// </summary>
    Task<LocalAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按用户 Id 查找账号（用于服务端数据范围解析）。不存在返回 null。
    /// </summary>
    Task<LocalAccount?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
