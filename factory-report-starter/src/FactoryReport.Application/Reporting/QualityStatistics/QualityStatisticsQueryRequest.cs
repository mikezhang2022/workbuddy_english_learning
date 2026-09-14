namespace FactoryReport.Application.Reporting.QualityStatistics;

/// <summary>
/// 质量统计（<c>quality_statistics</c>）只读查询输入。
/// 业务日期使用 <see cref="DateOnly"/> 闭区间语义（含起止日）。
/// <para>
/// 不包含 <c>workOrderCode</c>：当前领域 <c>ProductionRecord</c> 无工单维度，
/// 无法按工单过滤质量事实；正式 MES 是否支持【待现场确认】。
/// </para>
/// </summary>
public sealed class QualityStatisticsQueryRequest
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
