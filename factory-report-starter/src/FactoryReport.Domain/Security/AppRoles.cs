namespace FactoryReport.Domain.Security;

/// <summary>
/// 第一版固定角色编码（阶段 10）。组织数据范围强制与完整权限矩阵【待后续阶段 / 待现场确认】。
/// </summary>
public static class AppRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string FactoryAdmin = "FactoryAdmin";
    public const string ProductionManager = "ProductionManager";
    public const string QualityUser = "QualityUser";
    public const string Viewer = "Viewer";

    /// <summary>
    /// 本阶段允许签发的角色全集（不含规格中尚未落地的 ReportDesigner 等）。
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        SystemAdmin,
        FactoryAdmin,
        ProductionManager,
        QualityUser,
        Viewer
    ];

    public static bool IsKnown(string? role)
        => !string.IsNullOrWhiteSpace(role)
           && All.Contains(role.Trim(), StringComparer.Ordinal);
}
