using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Import;
using FactoryReport.Domain.Import;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// Fake 导入批次 / 数据版本只读仓储：委托 <see cref="IImportBatchWorkspace"/>。
/// 『仅用于开发测试，不代表现场 MES 正式口径』。
/// </summary>
public sealed class FakeImportBatchReadRepository : IImportBatchReadRepository
{
    private readonly IImportBatchWorkspace _workspace;

    public FakeImportBatchReadRepository(IImportBatchWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public Task<IReadOnlyList<ImportBatch>> GetImportBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        return _workspace.ListBatchesAsync(factoryId, datasetCode, cancellationToken);
    }

    public Task<IReadOnlyList<DatasetVersionState>> GetDatasetVersionsAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        return _workspace.GetDatasetVersionsAsync(factoryId, datasetCode, cancellationToken);
    }
}
