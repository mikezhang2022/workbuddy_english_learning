using FactoryReport.Application.Abstractions;

namespace FactoryReport.Application.Reporting.QualityStatistics;

/// <summary>
/// 质量统计聚合行。按「生产日期 + 组织范围 + 产品」汇总。
/// 数量分列展示，不把报废或返工自动合并到不良。
/// 『Fake 测试口径，现场 MES 接入前须确认』。
/// </summary>
public sealed class QualityStatisticsReportRow
{
    public DateOnly ProductionDate { get; init; }

    public long FactoryId { get; init; }
    public string FactoryCode { get; init; } = string.Empty;

    public long WorkshopId { get; init; }
    public string WorkshopCode { get; init; } = string.Empty;

    public long ProductionLineId { get; init; }
    public string ProductionLineCode { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    /// <summary>
    /// 检验数量（领域字段 InspectedQuantity 的 API 命名）。
    /// </summary>
    public decimal InspectionQuantity { get; init; }

    public decimal GoodQuantity { get; init; }

    /// <summary>不良数量；不与 Scrap/Rework 合并。『Fake 测试口径，现场 MES 接入前须确认』。</summary>
    public decimal DefectQuantity { get; init; }

    /// <summary>报废数量；分列展示，不自动并入 Defect。『Fake 测试口径，现场 MES 接入前须确认』。</summary>
    public decimal ScrapQuantity { get; init; }

    /// <summary>返工数量；分列展示，不自动并入 Defect。『Fake 测试口径，现场 MES 接入前须确认』。</summary>
    public decimal ReworkQuantity { get; init; }

    /// <summary>
    /// 良率。Fake 临时口径：GoodQuantity / InspectionQuantity；
    /// InspectionQuantity 为 0 时为 null。
    /// 『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public decimal? YieldRate { get; init; }

    /// <summary>
    /// 不良率。Fake 临时口径：DefectQuantity / InspectionQuantity；
    /// InspectionQuantity 为 0 时为 null。不把 Scrap/Rework 计入分子。
    /// 『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public decimal? DefectRate { get; init; }
}

/// <summary>
/// 查询筛选条件回显。不含 workOrderCode（ProductionRecord 无工单维度）。
/// </summary>
public sealed class QualityStatisticsFilterEcho
{
    public long FactoryId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public long? WorkshopId { get; init; }
    public long? ProductionLineId { get; init; }
    public string? ProductCode { get; init; }
}

/// <summary>
/// 响应元数据：稳定报表编码、数据模式、筛选回显与 Fake 口径声明。
/// </summary>
public sealed class QualityStatisticsReportMeta
{
    /// <summary>稳定报表编码，固定为 <c>quality_statistics</c>。</summary>
    public string ReportCode { get; init; } = StableReportCodes.QualityStatistics;

    /// <summary>数据访问模式（当前为 Fake）。</summary>
    public string DataAccessMode { get; init; } = nameof(Abstractions.DataAccessMode.Fake);

    public bool IsFake { get; init; } = true;

    public QualityStatisticsFilterEcho Filters { get; init; } = null!;

    /// <summary>
    /// Fake 良率公式说明。『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public string YieldRateFormula { get; init; } =
        "GoodQuantity / InspectionQuantity (null when InspectionQuantity = 0)";

    /// <summary>
    /// Fake 不良率公式说明。『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public string DefectRateFormula { get; init; } =
        "DefectQuantity / InspectionQuantity (null when InspectionQuantity = 0); Scrap/Rework not merged into Defect";

    /// <summary>口径声明：必须在客户端与文档中展示。</summary>
    public string QualityMetricsDisclaimer { get; init; } =
        "Fake 测试口径，现场 MES 接入前须确认";

    /// <summary>
    /// 说明本阶段未提供工单筛选的原因。
    /// </summary>
    public string WorkOrderFilterNote { get; init; } =
        "workOrderCode filter omitted: ProductionRecord has no work-order dimension in the current domain model.";

    public DateTimeOffset GeneratedAtUtc { get; init; }
}

/// <summary>
/// 质量统计查询响应。
/// </summary>
public sealed class QualityStatisticsQueryResponse
{
    public QualityStatisticsReportMeta Meta { get; init; } = null!;

    public IReadOnlyList<QualityStatisticsReportRow> Rows { get; init; } = [];
}
