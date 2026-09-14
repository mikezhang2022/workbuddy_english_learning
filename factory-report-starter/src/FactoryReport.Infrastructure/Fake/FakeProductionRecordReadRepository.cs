using FactoryReport.Application.DataAccess;
using FactoryReport.Domain.Production;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 生产事实仓储。『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeProductionRecordReadRepository : IProductionRecordReadRepository
{
    private readonly FakeFixtureSnapshot _snapshot;

    public FakeProductionRecordReadRepository(FakeFixtureSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public Task<IReadOnlyList<ProductionRecord>> GetProductionRecordsAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(dateRange);
        cancellationToken.ThrowIfCancellationRequested();

        var query = _snapshot.ProductionRecords
            .Where(r => r.FactoryId == scope.FactoryId)
            .Where(r => dateRange.Contains(r.ProductionDate));

        if (scope.WorkshopId is not null)
        {
            query = query.Where(r => r.WorkshopId == scope.WorkshopId.Value);
        }
        else if (scope.AuthorizedWorkshopIds is { Count: > 0 } workshops)
        {
            query = query.Where(r => workshops.Contains(r.WorkshopId));
        }

        if (scope.ProductionLineId is not null)
        {
            query = query.Where(r => r.ProductionLineId == scope.ProductionLineId.Value);
        }
        else if (scope.AuthorizedProductionLineIds is { Count: > 0 } lines)
        {
            query = query.Where(r => lines.Contains(r.ProductionLineId));
        }

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var code = productCode.Trim();
            query = query.Where(r => string.Equals(r.ProductCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<ProductionRecord> list = query.ToList();
        return Task.FromResult(list);
    }
}
