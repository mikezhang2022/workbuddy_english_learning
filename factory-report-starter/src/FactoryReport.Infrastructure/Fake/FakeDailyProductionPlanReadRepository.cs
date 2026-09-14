using FactoryReport.Application.DataAccess;
using FactoryReport.Domain.Planning;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 日计划仓储。『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeDailyProductionPlanReadRepository : IDailyProductionPlanReadRepository
{
    private readonly FakeFixtureSnapshot _snapshot;

    public FakeDailyProductionPlanReadRepository(FakeFixtureSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public Task<IReadOnlyList<DailyProductionPlanLine>> GetDailyPlanLinesAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(dateRange);
        cancellationToken.ThrowIfCancellationRequested();

        var query = _snapshot.DailyPlanLines
            .Where(p => p.FactoryId == scope.FactoryId)
            .Where(p => dateRange.Contains(p.PlanDate));

        if (scope.WorkshopId is not null)
        {
            query = query.Where(p => p.WorkshopId == scope.WorkshopId.Value);
        }
        else if (scope.AuthorizedWorkshopIds is { Count: > 0 } workshops)
        {
            query = query.Where(p => workshops.Contains(p.WorkshopId));
        }

        if (scope.ProductionLineId is not null)
        {
            query = query.Where(p => p.ProductionLineId == scope.ProductionLineId.Value);
        }
        else if (scope.AuthorizedProductionLineIds is { Count: > 0 } lines)
        {
            query = query.Where(p => p.ProductionLineId is not null && lines.Contains(p.ProductionLineId.Value));
        }

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var code = productCode.Trim();
            query = query.Where(p => string.Equals(p.ProductCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<DailyProductionPlanLine> list = query.ToList();
        return Task.FromResult(list);
    }
}
