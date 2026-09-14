namespace FactoryReport.Domain.Reporting;

/// <summary>
/// 报表系统稳定编码。一经发布不得随意变更；与 docs/data-dictionary.md 对齐。
/// 计划达成报表稳定编码为 <see cref="ProductionPlanAchievement"/>（字典组合数据集规划码 plan_achievement 的报表侧稳定标识）。
/// </summary>
public static class ReportCodes
{
    public const string ProductionDaily = "production_daily";
    public const string WorkOrderProgress = "work_order_progress";
    public const string QualityStatistics = "quality_statistics";
    public const string ProductionPlanAchievement = "production_plan_achievement";
    public const string MonthlyProductionPlan = "monthly_production_plan";

    public static IReadOnlyList<string> All { get; } =
    [
        ProductionDaily,
        WorkOrderProgress,
        QualityStatistics,
        ProductionPlanAchievement,
        MonthlyProductionPlan
    ];

    public static bool IsKnown(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        foreach (var known in All)
        {
            if (string.Equals(known, normalized, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
