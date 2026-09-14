using FactoryReport.Application.Reporting.ProductionPlanAchievement;

namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 当前筛选返回行的前端展示汇总（非正式统计口径；以后端/API 为准）。
/// 仅合计计划/实际数量；不重算达成率。
/// </summary>
public sealed class ProductionPlanAchievementDisplaySummary
{
    /// <summary>已配置计划行的计划数量合计（跳过 PlanQuantity=null）。</summary>
    public decimal PlanQuantity { get; init; }

    public decimal ActualQuantity { get; init; }

    public int RowCount { get; init; }

    public static ProductionPlanAchievementDisplaySummary FromRows(
        IReadOnlyList<ProductionPlanAchievementReportRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return new ProductionPlanAchievementDisplaySummary();
        }

        return new ProductionPlanAchievementDisplaySummary
        {
            RowCount = rows.Count,
            PlanQuantity = rows.Where(r => r.PlanQuantity is not null).Sum(r => r.PlanQuantity!.Value),
            ActualQuantity = rows.Sum(r => r.ActualQuantity)
        };
    }
}
