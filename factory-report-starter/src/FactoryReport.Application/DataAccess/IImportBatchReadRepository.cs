using FactoryReport.Domain.Import;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 导入批次 / 数据版本只读访问。未来 Oracle 实现【待现场确认】。
/// </summary>
public interface IImportBatchReadRepository
{
    Task<IReadOnlyList<ImportBatch>> GetImportBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatasetVersionState>> GetDatasetVersionsAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default);
}
