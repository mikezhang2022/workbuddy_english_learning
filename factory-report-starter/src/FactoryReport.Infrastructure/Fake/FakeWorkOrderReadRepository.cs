using FactoryReport.Application.DataAccess;
using FactoryReport.Domain.MasterData;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 工单仓储。『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeWorkOrderReadRepository : IWorkOrderReadRepository
{
    private readonly FakeFixtureSnapshot _snapshot;

    public FakeWorkOrderReadRepository(FakeFixtureSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(
        OrganizationScopeFilter scope,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        var query = _snapshot.WorkOrders.Where(w => w.FactoryId == scope.FactoryId);
        if (scope.WorkshopId is not null)
        {
            query = query.Where(w => w.WorkshopId == scope.WorkshopId);
        }

        if (scope.ProductionLineId is not null)
        {
            query = query.Where(w => w.ProductionLineId == scope.ProductionLineId);
        }

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var code = productCode.Trim();
            query = query.Where(w => string.Equals(w.ProductCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<WorkOrder> list = query.ToList();
        return Task.FromResult(list);
    }
}
