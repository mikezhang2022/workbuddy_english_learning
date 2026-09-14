using FactoryReport.Domain.Import;
using FactoryReport.Domain.Reporting;

namespace FactoryReport.Application.Reporting.MonthlyProductionPlan;

/// <summary>
/// 月度生产计划日行。必须保留 PlanDate；数量为月度文件内的原始日计划，不得由月总量均摊。
/// </summary>
public sealed class MonthlyProductionPlanReportRow
{
    public DateOnly PlanDate { get; init; }

    public long FactoryId { get; init; }
    public string FactoryCode { get; init; } = string.Empty;

    public long WorkshopId { get; init; }
    public string WorkshopCode { get; init; } = string.Empty;

    public long? ProductionLineId { get; init; }
    public string? ProductionLineCode { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    /// <summary>日计划数量（文件内原始行，非月均摊）。</summary>
    public decimal PlanQuantity { get; init; }

    public Guid PlanVersionId { get; init; }
    public string PlanVersionNo { get; init; } = string.Empty;

    /// <summary>发布状态字符串（Published / Draft 等）。正式枚举【待现场确认】。</summary>
    public string PublishStatus { get; init; } = nameof(DatasetPublishStatus.Published);

    public bool IsActive { get; init; }

    public string? Remark { get; init; }
}

/// <summary>查询筛选条件回显。</summary>
public sealed class MonthlyProductionPlanFilterEcho
{
    public long FactoryId { get; init; }
    public string PlanMonth { get; init; } = string.Empty;
    public long? WorkshopId { get; init; }
    public long? ProductionLineId { get; init; }
    public string? ProductCode { get; init; }
}

/// <summary>
/// 响应元数据：稳定报表编码、Fake 模式与 Fake 版本过滤规则说明。
/// </summary>
public sealed class MonthlyProductionPlanReportMeta
{
    /// <summary>稳定报表编码，固定为 <c>monthly_production_plan</c>。</summary>
    public string ReportCode { get; init; } = StableReportCodes.MonthlyProductionPlan;

    /// <summary>数据访问模式（当前为 Fake）。</summary>
    public string DataAccessMode { get; init; } = nameof(Abstractions.DataAccessMode.Fake);

    public bool IsFake { get; init; } = true;

    public MonthlyProductionPlanFilterEcho Filters { get; init; } = null!;

    /// <summary>当前可见（Published + Active）计划版本 Id；无则 null。</summary>
    public Guid? ActivePlanVersionId { get; init; }

    /// <summary>当前可见计划版本号。</summary>
    public string? ActivePlanVersionNo { get; init; }

    /// <summary>数据来源标识（当前 Fake 夹具版本标签）。</summary>
    public string DataSourceIdentifier { get; init; } = "FakeFixture:DeterministicFakeFixture";

    /// <summary>
    /// Fake 版本过滤规则说明（非正式现场规则）。
    /// </summary>
    public string FakeVersionRuleNote { get; init; } =
        "FAKE DATA VERSION BEHAVIOR ONLY: return rows from DatasetVersion where PublishStatus=Published AND IsActive=true. " +
        "Real Excel publish, version activation, and rollback rules will be implemented in later phases and must be confirmed on-site 【待现场确认】.";

    /// <summary>日计划行原则：不得用月计划平均推算日计划。</summary>
    public string DailyPlanLinePrinciple { get; init; } =
        "Return DailyProductionPlanLine rows from the monthly file as-is (retain PlanDate). " +
        "Do not average or allocate monthly totals across days.";

    public DateTimeOffset GeneratedAtUtc { get; init; }
}

/// <summary>月度生产计划查询响应。</summary>
public sealed class MonthlyProductionPlanQueryResponse
{
    public MonthlyProductionPlanReportMeta Meta { get; init; } = null!;

    public IReadOnlyList<MonthlyProductionPlanReportRow> Rows { get; init; } = [];
}
