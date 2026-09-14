namespace FactoryReport.Application.Reporting.MonthlyProductionPlan;

/// <summary>
/// 月度生产计划（<c>monthly_production_plan</c>）只读查询输入。
/// <see cref="PlanMonth"/> 为 yyyy-MM；返回该月文件内的日计划行（保留 PlanDate），不得月均摊到日。
/// </summary>
public sealed class MonthlyProductionPlanQueryRequest
{
    /// <summary>工厂 ID（必填，正整数）。</summary>
    public long? FactoryId { get; init; }

    /// <summary>计划月份（必填，yyyy-MM）。</summary>
    public string? PlanMonth { get; init; }

    /// <summary>可选车间 ID。</summary>
    public long? WorkshopId { get; init; }

    /// <summary>可选产线 ID。</summary>
    public long? ProductionLineId { get; init; }

    /// <summary>可选产品编码（精确匹配）。</summary>
    public string? ProductCode { get; init; }
}
