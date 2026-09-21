using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Import;
using FactoryReport.Domain.Planning;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 日计划仓储：夹具行 + 导入发布行合并。『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeDailyProductionPlanReadRepository : IDailyProductionPlanReadRepository
{
    private readonly FakeFixtureSnapshot _snapshot;
    private readonly IImportBatchWorkspace _workspace;

    public FakeDailyProductionPlanReadRepository(
        FakeFixtureSnapshot snapshot,
        IImportBatchWorkspace workspace)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public async Task<IReadOnlyList<DailyProductionPlanLine>> GetDailyPlanLinesAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(dateRange);
        cancellationToken.ThrowIfCancellationRequested();

        var imported = await _workspace.GetPublishedPlanLinesAsync(cancellationToken).ConfigureAwait(false);
        var combined = _snapshot.DailyPlanLines.Concat(imported);

        var query = combined
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
        return list;
    }
}
