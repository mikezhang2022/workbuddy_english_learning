using FactoryReport.Domain.Import;
using FactoryReport.Domain.MasterData;
using FactoryReport.Domain.Organizations;
using FactoryReport.Domain.Planning;
using FactoryReport.Domain.Production;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 面向后续报表引擎的聚合查询服务：按 FactoryId、日期范围与组织范围读取数据。
/// 不包含报表 API / 页面 / 认证。实现由 Infrastructure Fake（或未来 Oracle）仓储支撑。
/// </summary>
public interface IReportDataQueryService
{
    Task<IReadOnlyList<Factory>> GetFactoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Workshop>> GetWorkshopsAsync(long factoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductionLine>> GetProductionLinesAsync(
        long factoryId,
        long? workshopId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetProductsAsync(
        long factoryId,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(
        OrganizationScopeFilter scope,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductionRecord>> GetProductionRecordsAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyProductionPlanLine>> GetDailyPlanLinesAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportBatch>> GetImportBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatasetVersionState>> GetDatasetVersionsAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default);
}
