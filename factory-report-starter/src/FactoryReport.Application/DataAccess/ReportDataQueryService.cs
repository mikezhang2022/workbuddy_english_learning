using FactoryReport.Domain.Import;
using FactoryReport.Domain.MasterData;
using FactoryReport.Domain.Organizations;
using FactoryReport.Domain.Planning;
using FactoryReport.Domain.Production;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 报表数据查询服务：委托只读仓储，保持 Application 不依赖 Infrastructure。
/// </summary>
public sealed class ReportDataQueryService : IReportDataQueryService
{
    private readonly IOrganizationReadRepository _organizations;
    private readonly IProductReadRepository _products;
    private readonly IWorkOrderReadRepository _workOrders;
    private readonly IProductionRecordReadRepository _productionRecords;
    private readonly IDailyProductionPlanReadRepository _dailyPlans;
    private readonly IImportBatchReadRepository _importBatches;

    public ReportDataQueryService(
        IOrganizationReadRepository organizations,
        IProductReadRepository products,
        IWorkOrderReadRepository workOrders,
        IProductionRecordReadRepository productionRecords,
        IDailyProductionPlanReadRepository dailyPlans,
        IImportBatchReadRepository importBatches)
    {
        _organizations = organizations ?? throw new ArgumentNullException(nameof(organizations));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _workOrders = workOrders ?? throw new ArgumentNullException(nameof(workOrders));
        _productionRecords = productionRecords ?? throw new ArgumentNullException(nameof(productionRecords));
        _dailyPlans = dailyPlans ?? throw new ArgumentNullException(nameof(dailyPlans));
        _importBatches = importBatches ?? throw new ArgumentNullException(nameof(importBatches));
    }

    public Task<IReadOnlyList<Factory>> GetFactoriesAsync(CancellationToken cancellationToken = default) =>
        _organizations.GetFactoriesAsync(cancellationToken);

    public Task<IReadOnlyList<Workshop>> GetWorkshopsAsync(long factoryId, CancellationToken cancellationToken = default) =>
        _organizations.GetWorkshopsAsync(factoryId, cancellationToken);

    public Task<IReadOnlyList<ProductionLine>> GetProductionLinesAsync(
        long factoryId,
        long? workshopId = null,
        CancellationToken cancellationToken = default) =>
        _organizations.GetProductionLinesAsync(factoryId, workshopId, cancellationToken);

    public Task<IReadOnlyList<Product>> GetProductsAsync(
        long factoryId,
        string? productCode = null,
        CancellationToken cancellationToken = default) =>
        _products.GetProductsAsync(factoryId, productCode, cancellationToken);

    public Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(
        OrganizationScopeFilter scope,
        string? productCode = null,
        CancellationToken cancellationToken = default) =>
        _workOrders.GetWorkOrdersAsync(scope, productCode, cancellationToken);

    public Task<IReadOnlyList<ProductionRecord>> GetProductionRecordsAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default) =>
        _productionRecords.GetProductionRecordsAsync(scope, dateRange, productCode, cancellationToken);

    public Task<IReadOnlyList<DailyProductionPlanLine>> GetDailyPlanLinesAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default) =>
        _dailyPlans.GetDailyPlanLinesAsync(scope, dateRange, productCode, cancellationToken);

    public Task<IReadOnlyList<ImportBatch>> GetImportBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default) =>
        _importBatches.GetImportBatchesAsync(factoryId, datasetCode, cancellationToken);

    public Task<IReadOnlyList<DatasetVersionState>> GetDatasetVersionsAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default) =>
        _importBatches.GetDatasetVersionsAsync(factoryId, datasetCode, cancellationToken);
}
