namespace FactoryReport.Application.Reporting.WorkOrderProgress;

/// <summary>
/// 工单进度（<c>work_order_progress</c>）只读查询输入。
/// 计划完成日期范围使用 <see cref="DateOnly"/> 闭区间，按 PlannedFinishUtc 的 UTC 日历日比较。
/// </summary>
public sealed class WorkOrderProgressQueryRequest
{
    /// <summary>工厂 ID（必填，正整数）。越权/越范围不得串工厂数据。</summary>
    public long? FactoryId { get; init; }

    /// <summary>可选车间 ID。</summary>
    public long? WorkshopId { get; init; }

    /// <summary>可选产线 ID。</summary>
    public long? ProductionLineId { get; init; }

    /// <summary>可选产品编码（精确匹配）。</summary>
    public string? ProductCode { get; init; }

    /// <summary>可选工单号（精确匹配；领域 WorkOrderNo）。</summary>
    public string? WorkOrderCode { get; init; }

    /// <summary>
    /// 可选状态筛选（精确匹配 StatusCode）。
    /// Fake 临时值示例：Open / Completed / Closed；正式枚举【待现场确认】。
    /// </summary>
    public string? Status { get; init; }

    /// <summary>可选计划完成日起始日（UTC 日历日，闭区间）。</summary>
    public DateOnly? PlannedFinishFrom { get; init; }

    /// <summary>可选计划完成日结束日（UTC 日历日，闭区间）；不得早于 <see cref="PlannedFinishFrom"/>。</summary>
    public DateOnly? PlannedFinishTo { get; init; }
}
