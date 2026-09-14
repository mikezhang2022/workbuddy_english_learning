namespace FactoryReport.Application.Reporting.QualityStatistics;

/// <summary>
/// 质量统计（quality_statistics）只读查询服务。
/// 仅通过 <c>IReportDataQueryService</c> 读取数据，不依赖 Fake 实现类。
/// </summary>
public interface IQualityStatisticsReportService
{
    Task<QualityStatisticsQueryResponse> QueryAsync(
        QualityStatisticsQueryRequest request,
        CancellationToken cancellationToken = default);
}
