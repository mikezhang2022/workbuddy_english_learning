using FactoryReport.Application.DataAccess;
using FactoryReport.Domain.MasterData;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 产品仓储。『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeProductReadRepository : IProductReadRepository
{
    private readonly FakeFixtureSnapshot _snapshot;

    public FakeProductReadRepository(FakeFixtureSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public Task<IReadOnlyList<Product>> GetProductsAsync(
        long factoryId,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        var query = _snapshot.Products.Where(p => p.FactoryId == factoryId);
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var code = productCode.Trim();
            query = query.Where(p => string.Equals(p.ProductCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<Product> list = query.ToList();
        return Task.FromResult(list);
    }
}
