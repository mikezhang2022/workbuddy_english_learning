using FactoryReport.Application.Abstractions;

namespace FactoryReport.Application.Reporting.WorkOrderProgress;

/// <summary>
/// 工单进度行。
/// </summary>
public sealed class WorkOrderProgressReportRow
{
    public long FactoryId { get; init; }
    public string FactoryCode { get; init; } = string.Empty;

    public long? WorkshopId { get; init; }
    public string? WorkshopCode { get; init; }

    public long? ProductionLineId { get; init; }
    public string? ProductionLineCode { get; init; }

    /// <summary>工单业务编号（领域 WorkOrderNo；始终按文本）。</summary>
    public string WorkOrderCode { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    /// <summary>
    /// 工单状态编码。Fake 临时值：Open / Completed / Closed。
    /// 『仅用于开发测试，不代表现场 MES 正式枚举』；正式【待现场确认】。
    /// </summary>
    public string? Status { get; init; }

    /// <summary>计划数量（领域 PlanQuantity）。</summary>
    public decimal PlannedQuantity { get; init; }

    /// <summary>实际/完成数量（领域 CompletedQuantity）。</summary>
    public decimal ActualQuantity { get; init; }

    /// <summary>
    /// 剩余数量。Fake：max(PlannedQuantity - ActualQuantity, 0)。
    /// 『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public decimal RemainingQuantity { get; init; }

    /// <summary>
    /// 完成率。Fake：ActualQuantity / PlannedQuantity；PlannedQuantity 为 0 时为 null（同达成率零分母口径）。
    /// 超报时可 &gt; 100%。『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public decimal? CompletionRate { get; init; }

    public DateTimeOffset? PlannedStartUtc { get; init; }
    public DateTimeOffset? PlannedFinishUtc { get; init; }

    /// <summary>
    /// Fake：Status 为 Completed 或 Closed（忽略大小写）时为 true。
    /// 『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Fake 延期：未完成/未关闭，且比较时间晚于 PlannedFinishUtc。
    /// 『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public bool IsOverdue { get; init; }
}

/// <summary>
/// 查询筛选条件回显。
/// </summary>
public sealed class WorkOrderProgressFilterEcho
{
    public long FactoryId { get; init; }
    public long? WorkshopId { get; init; }
    public long? ProductionLineId { get; init; }
    public string? ProductCode { get; init; }
    public string? WorkOrderCode { get; init; }
    public string? Status { get; init; }
    public DateOnly? PlannedFinishFrom { get; init; }
    public DateOnly? PlannedFinishTo { get; init; }
}

/// <summary>
/// 响应元数据：稳定报表编码、数据模式、筛选回显与 Fake 延期口径声明。
/// </summary>
public sealed class WorkOrderProgressReportMeta
{
    /// <summary>稳定报表编码，固定为 <c>work_order_progress</c>。</summary>
    public string ReportCode { get; init; } = StableReportCodes.WorkOrderProgress;

    /// <summary>数据访问模式（当前为 Fake）。</summary>
    public string DataAccessMode { get; init; } = nameof(Abstractions.DataAccessMode.Fake);

    public bool IsFake { get; init; } = true;

    public WorkOrderProgressFilterEcho Filters { get; init; } = null!;

    /// <summary>
    /// Fake 延期判定说明。『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public string OverdueRule { get; init; } =
        "IsOverdue = (!IsCompleted) AND (PlannedFinishUtc is not null) AND (UtcNow > PlannedFinishUtc); " +
        "IsCompleted when Status is Completed or Closed (Fake temporary status codes).";

    /// <summary>口径声明：必须在客户端与文档中展示。</summary>
    public string OverdueDisclaimer { get; init; } =
        "Fake 测试规则：现场须确认工单状态枚举、时区、延期口径与计划时间来源";

    /// <summary>
    /// Fake 完成率说明。PlannedQuantity=0 → null（参照达成率零分母）。
    /// </summary>
    public string CompletionRateRule { get; init; } =
        "CompletionRate = ActualQuantity / PlannedQuantity (null when PlannedQuantity = 0); may exceed 100%.";

    /// <summary>本次比较所用的「当前 UTC 时间」（来自 IUtcClock，可注入）。</summary>
    public DateTimeOffset ComparedAtUtc { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }
}

/// <summary>
/// 工单进度查询响应。
/// </summary>
public sealed class WorkOrderProgressQueryResponse
{
    public WorkOrderProgressReportMeta Meta { get; init; } = null!;

    public IReadOnlyList<WorkOrderProgressReportRow> Rows { get; init; } = [];
}
