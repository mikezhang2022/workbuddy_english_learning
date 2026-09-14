namespace FactoryReport.Application.Reporting.ProductionDaily;

/// <summary>
/// 生产日报（production_daily）只读查询服务。
/// 仅通过 <c>IReportDataQueryService</c> 读取数据，不依赖 Fake 实现类。
/// </summary>
public interface IProductionDailyReportService
{
    Task<ProductionDailyQueryResponse> QueryAsync(
        ProductionDailyQueryRequest request,
        CancellationToken cancellationToken = default);
}
