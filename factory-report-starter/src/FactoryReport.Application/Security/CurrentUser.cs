namespace FactoryReport.Application.Security;

/// <summary>
/// 当前已认证用户快照（不含密码、不含细粒度权限明细）。
/// </summary>
public sealed class CurrentUser
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public bool IsAuthenticated { get; init; } = true;
}
