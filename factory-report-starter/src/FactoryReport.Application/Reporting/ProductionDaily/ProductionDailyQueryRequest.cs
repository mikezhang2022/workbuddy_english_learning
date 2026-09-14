namespace FactoryReport.Application.Reporting.ProductionDaily;

/// <summary>
/// 生产日报（<c>production_daily</c>）只读查询输入。
/// 业务日期使用 <see cref="DateOnly"/> 闭区间语义（含起止日）。
/// </summary>
public sealed class ProductionDailyQueryRequest
{
    /// <summary>工厂 ID（必填，正整数）。越权/越范围不得串工厂数据。</summary>
    public long? FactoryId { get; init; }

    /// <summary>生产日起始日（必填，闭区间）。</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>生产日结束日（必填，闭区间）；不得早于 <see cref="StartDate"/>。</summary>
    public DateOnly? EndDate { get; init; }

    /// <summary>可选车间 ID。</summary>
    public long? WorkshopId { get; init; }

    /// <summary>可选产线 ID。</summary>
    public long? ProductionLineId { get; init; }

    /// <summary>可选产品编码（精确匹配）。</summary>
    public string? ProductCode { get; init; }
}
