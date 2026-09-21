using FactoryReport.Domain.Import;
using FactoryReport.Domain.Planning;

namespace FactoryReport.Application.Import;

/// <summary>
/// 导入批次可写存储（Fake 内存实现）。与 Oracle 业务表职责分离；不得写入 MES/Oracle 业务表。
/// </summary>
public interface IImportBatchWorkspace
{
    Task<ImportBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default);

    Task SaveBatchAsync(ImportBatch batch, CancellationToken cancellationToken = default);

    Task ReplaceErrorsAsync(
        Guid batchId,
        IReadOnlyList<ImportRowError> errors,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportRowError>> GetErrorsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task ReplaceParsedRowsAsync(
        Guid batchId,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> GetParsedRowsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatasetVersionState>> GetDatasetVersionsAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default);

    Task UpsertDatasetVersionAsync(DatasetVersionState version, CancellationToken cancellationToken = default);

    Task SetBatchPublishedVersionAsync(
        Guid batchId,
        Guid datasetVersionId,
        Guid? previousActiveVersionId,
        CancellationToken cancellationToken = default);

    Task<(Guid? PublishedVersionId, Guid? PreviousActiveVersionId)> GetBatchVersionLinksAsync(
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task ReplacePublishedPlanLinesAsync(
        Guid datasetVersionId,
        IReadOnlyList<DailyProductionPlanLine> lines,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyProductionPlanLine>> GetPublishedPlanLinesAsync(
        CancellationToken cancellationToken = default);

    Task ReplacePublishedActualRowsAsync(
        Guid datasetVersionId,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        CancellationToken cancellationToken = default);
}
