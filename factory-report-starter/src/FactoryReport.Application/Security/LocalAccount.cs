namespace FactoryReport.Application.Security;

/// <summary>
/// 本地账号只读投影（验证用）。密码以哈希形式保存，禁止明文。
/// </summary>
public sealed class LocalAccount
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>
    /// 密码哈希（PBKDF2 / ASP.NET Identity 兼容格式）。不得为明文。
    /// </summary>
    public required string PasswordHash { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>
    /// true 表示仅供开发/自动化测试的 Fake 账号，不得当作正式生产账号。
    /// </summary>
    public bool IsTestOnlyAccount { get; init; }
}
