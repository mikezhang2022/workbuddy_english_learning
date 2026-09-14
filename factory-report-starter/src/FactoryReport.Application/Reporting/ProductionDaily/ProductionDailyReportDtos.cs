using FactoryReport.Application.Abstractions;

namespace FactoryReport.Application.Reporting.ProductionDaily;

/// <summary>
/// 生产日报聚合行。按「生产日期 + 组织范围 + 产品」汇总。
/// </summary>
public sealed class ProductionDailyReportRow
{
    public DateOnly ProductionDate { get; init; }

    public long FactoryId { get; init; }
    public string FactoryCode { get; init; } = string.Empty;

    public long WorkshopId { get; init; }
    public string WorkshopCode { get; init; } = string.Empty;

    public long ProductionLineId { get; init; }
    public string ProductionLineCode { get; init; } = string.Empty;

    public string ProductCode { get; init; } = string.Empty;

    public decimal ActualQuantity { get; init; }
    public decimal GoodQuantity { get; init; }
    public decimal DefectQuantity { get; init; }
    public decimal ScrapQuantity { get; init; }
    public decimal ReworkQuantity { get; init; }

    /// <summary>
    /// 检验数量（领域字段 InspectedQuantity 的 API 命名）。
    /// </summary>
    public decimal InspectionQuantity { get; init; }

    /// <summary>
    /// 良率。Fake 临时口径：GoodQuantity / InspectionQuantity；
    /// InspectionQuantity 为 0 时为 null。
    /// 『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public decimal? YieldRate { get; init; }
}

/// <summary>
/// 查询筛选条件回显。
/// </summary>
public sealed class ProductionDailyFilterEcho
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
public sealed class ProductionDailyReportMeta
{
    /// <summary>稳定报表编码，固定为 <c>production_daily</c>。</summary>
    public string ReportCode { get; init; } = StableReportCodes.ProductionDaily;

    /// <summary>数据访问模式（当前为 Fake）。</summary>
    public string DataAccessMode { get; init; } = nameof(Abstractions.DataAccessMode.Fake);

    public bool IsFake { get; init; } = true;

    public ProductionDailyFilterEcho Filters { get; init; } = null!;

    /// <summary>
    /// Fake 良率公式说明。『Fake 测试口径，现场 MES 接入前须确认』。
    /// </summary>
    public string YieldRateFormula { get; init; } =
        "GoodQuantity / InspectionQuantity (null when InspectionQuantity = 0)";

    /// <summary>口径声明：必须在客户端与文档中展示。</summary>
    public string YieldRateDisclaimer { get; init; } =
        "Fake 测试口径，现场 MES 接入前须确认";

    public DateTimeOffset GeneratedAtUtc { get; init; }
}

/// <summary>
/// 生产日报查询响应。
/// </summary>
public sealed class ProductionDailyQueryResponse
{
    public ProductionDailyReportMeta Meta { get; init; } = null!;

    public IReadOnlyList<ProductionDailyReportRow> Rows { get; init; } = [];
}
