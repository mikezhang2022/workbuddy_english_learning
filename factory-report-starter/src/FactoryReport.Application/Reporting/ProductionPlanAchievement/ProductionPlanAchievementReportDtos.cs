using FactoryReport.Domain.Reporting;

namespace FactoryReport.Application.Reporting.ProductionPlanAchievement;

/// <summary>
/// 生产计划达成行。关联键：FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode。
/// 达成率边界规则为已确认产品规则（非 Fake 临时口径）。
/// </summary>
public sealed class ProductionPlanAchievementReportRow
{
    public DateOnly ProductionDate { get; init; }

    public long FactoryId { get; init; }
    public string FactoryCode { get; init; } = string.Empty;

    public long WorkshopId { get; init; }
    public string WorkshopCode { get; init; } = string.Empty;

    public long ProductionLineId { get; init; }
    public string ProductionLineCode { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    /// <summary>计划数量；未配置计划时为 null。</summary>
    public decimal? PlanQuantity { get; init; }

    /// <summary>实际数量；有计划无实际时为 0。</summary>
    public decimal ActualQuantity { get; init; }

    /// <summary>
    /// 达成率。有计划且计划 &gt; 0：Actual / Plan；
    /// 计划为 0 或未配置计划：null；有计划无实际：0。
    /// </summary>
    public decimal? AchievementRate { get; init; }

    /// <summary>
    /// 计划达成状态：Calculated / PlanIsZero / PlanNotConfigured / MissingActual。
    /// （阶段说明中的 PlanQuantityZero 对应领域枚举 PlanIsZero。）
    /// </summary>
    public string PlanStatus { get; init; } = nameof(PlanAchievementStatus.Calculated);
}

/// <summary>查询筛选条件回显。</summary>
public sealed class ProductionPlanAchievementFilterEcho
{
    public long FactoryId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public long? WorkshopId { get; init; }
    public long? ProductionLineId { get; init; }
    public string? ProductCode { get; init; }
}

/// <summary>
/// 响应元数据：稳定报表编码、数据模式、关联键与计算口径说明。
/// </summary>
public sealed class ProductionPlanAchievementReportMeta
{
    /// <summary>稳定报表编码，固定为 <c>production_plan_achievement</c>。</summary>
    public string ReportCode { get; init; } = StableReportCodes.ProductionPlanAchievement;

    /// <summary>数据访问模式（当前为 Fake）。</summary>
    public string DataAccessMode { get; init; } = nameof(Abstractions.DataAccessMode.Fake);

    public bool IsFake { get; init; } = true;

    public ProductionPlanAchievementFilterEcho Filters { get; init; } = null!;

    /// <summary>已确认关联键说明。</summary>
    public string MatchKey { get; init; } =
        "FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode";

    /// <summary>已确认达成率计算口径说明（非 Fake 临时规则）。</summary>
    public string AchievementRules { get; init; } =
        "Plan>0: AchievementRate=Actual/Plan (Calculated); " +
        "Plan=0: AchievementRate=null (PlanIsZero); " +
        "Actual without plan: AchievementRate=null (PlanNotConfigured, never show 0%); " +
        "Plan without actual: Actual=0, AchievementRate=0 (MissingActual); " +
        "Do not derive daily plan from monthly average.";

    /// <summary>数据状态说明：当前读数来自 Fake 夹具，边界计算规则为已确认产品规则。</summary>
    public string DataStatusNote { get; init; } =
        "Boundary achievement rules are confirmed product rules; source quantities currently come from Fake fixtures (not MES/Oracle).";

    public DateTimeOffset GeneratedAtUtc { get; init; }
}

/// <summary>生产计划达成查询响应。</summary>
public sealed class ProductionPlanAchievementQueryResponse
{
    public ProductionPlanAchievementReportMeta Meta { get; init; } = null!;

    public IReadOnlyList<ProductionPlanAchievementReportRow> Rows { get; init; } = [];
}
