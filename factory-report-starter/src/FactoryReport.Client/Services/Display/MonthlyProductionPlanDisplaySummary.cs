using FactoryReport.Application.Reporting.MonthlyProductionPlan;

namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 当前筛选返回日计划行的前端展示合计（非正式统计口径；以后端/API 为准）。
/// 仅合计原始 PlanQuantity，不做月均摊或按日推算。
/// </summary>
public sealed class MonthlyProductionPlanDisplaySummary
{
    public decimal PlanQuantity { get; init; }

    public int RowCount { get; init; }

    public static MonthlyProductionPlanDisplaySummary FromRows(
        IReadOnlyList<MonthlyProductionPlanReportRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return new MonthlyProductionPlanDisplaySummary();
        }

        return new MonthlyProductionPlanDisplaySummary
        {
            RowCount = rows.Count,
            PlanQuantity = rows.Sum(r => r.PlanQuantity)
        };
    }
}
