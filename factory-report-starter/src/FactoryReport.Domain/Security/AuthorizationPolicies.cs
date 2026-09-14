namespace FactoryReport.Domain.Security;

/// <summary>
/// 功能权限策略名称（阶段 10 仅定义；现有报表 API 暂不 RequireAuthorization）。
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireAuthenticated = "RequireAuthenticated";
    public const string RequireSystemAdmin = "RequireSystemAdmin";
    public const string RequireFactoryAdmin = "RequireFactoryAdmin";
    public const string RequireProductionManager = "RequireProductionManager";
    public const string RequireQualityUser = "RequireQualityUser";
    public const string RequireViewer = "RequireViewer";

    /// <summary>
    /// 可查看生产类报表的角色集合策略名（后续报表授权用）。
    /// </summary>
    public const string CanViewProductionReports = "CanViewProductionReports";

    /// <summary>
    /// 可查看质量类报表的角色集合策略名（后续报表授权用）。
    /// </summary>
    public const string CanViewQualityReports = "CanViewQualityReports";
}
