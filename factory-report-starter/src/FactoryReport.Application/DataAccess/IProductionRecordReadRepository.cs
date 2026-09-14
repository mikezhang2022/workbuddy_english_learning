using FactoryReport.Domain.Production;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 生产事实只读访问。必须带 FactoryId 与日期范围。未来 Oracle 实现【待现场确认】。
/// </summary>
public interface IProductionRecordReadRepository
{
    Task<IReadOnlyList<ProductionRecord>> GetProductionRecordsAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default);
}
