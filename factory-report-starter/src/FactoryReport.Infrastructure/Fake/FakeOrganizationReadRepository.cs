using FactoryReport.Application.DataAccess;
using FactoryReport.Domain.Organizations;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 组织仓储：仅进程内夹具，不连接 Oracle / 文件 / 网络。
/// 『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeOrganizationReadRepository : IOrganizationReadRepository
{
    private readonly FakeFixtureSnapshot _snapshot;

    public FakeOrganizationReadRepository(FakeFixtureSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public Task<IReadOnlyList<Factory>> GetFactoriesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_snapshot.Factories);
    }

    public Task<Factory?> GetFactoryByIdAsync(long factoryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var factory = _snapshot.Factories.FirstOrDefault(f => f.Id == factoryId);
        return Task.FromResult(factory);
    }

    public Task<IReadOnlyList<Workshop>> GetWorkshopsAsync(long factoryId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Workshop> list = _snapshot.Workshops.Where(w => w.FactoryId == factoryId).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<ProductionLine>> GetProductionLinesAsync(
        long factoryId,
        long? workshopId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = _snapshot.ProductionLines.Where(l => l.FactoryId == factoryId);
        if (workshopId is not null)
        {
            query = query.Where(l => l.WorkshopId == workshopId.Value);
        }

        IReadOnlyList<ProductionLine> list = query.ToList();
        return Task.FromResult(list);
    }
}
