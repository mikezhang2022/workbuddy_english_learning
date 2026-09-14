namespace FactoryReport.Domain.Security;

/// <summary>
/// 功能权限策略名称。阶段 11：报表 API 使用 <see cref="ReportRead"/>。
/// 更细粒度「角色 × 报表」矩阵【待后续确认】，本阶段不虚构限制。
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
    /// 报表只读：五个角色均可。组织数据范围另由服务端 Scope 强制。
    /// </summary>
    public const string ReportRead = "ReportRead";

    /// <summary>
    /// 可查看生产类报表的角色集合策略名（细粒度矩阵【待后续确认】；本阶段报表统一用 ReportRead）。
    /// </summary>
    public const string CanViewProductionReports = "CanViewProductionReports";

    /// <summary>
    /// 可查看质量类报表的角色集合策略名（细粒度矩阵【待后续确认】；本阶段报表统一用 ReportRead）。
    /// </summary>
    public const string CanViewQualityReports = "CanViewQualityReports";
}
