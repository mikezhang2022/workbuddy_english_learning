using FactoryReport.Application.DataAccess;
using FactoryReport.Domain.Import;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 导入批次 / 数据版本仓储。『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeImportBatchReadRepository : IImportBatchReadRepository
{
    private readonly FakeFixtureSnapshot _snapshot;

    public FakeImportBatchReadRepository(FakeFixtureSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public Task<IReadOnlyList<ImportBatch>> GetImportBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        var query = _snapshot.ImportBatches.Where(b => b.FactoryId == factoryId);
        if (!string.IsNullOrWhiteSpace(datasetCode))
        {
            var code = datasetCode.Trim();
            query = query.Where(b => string.Equals(b.DatasetCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<ImportBatch> list = query.ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<DatasetVersionState>> GetDatasetVersionsAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        var query = _snapshot.DatasetVersions.Where(v => v.FactoryId == factoryId);
        if (!string.IsNullOrWhiteSpace(datasetCode))
        {
            var code = datasetCode.Trim();
            query = query.Where(v => string.Equals(v.DatasetCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<DatasetVersionState> list = query.ToList();
        return Task.FromResult(list);
    }
}
