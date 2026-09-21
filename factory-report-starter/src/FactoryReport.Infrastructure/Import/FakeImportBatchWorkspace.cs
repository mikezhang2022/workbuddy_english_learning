using System.Collections.Concurrent;
using FactoryReport.Application.Import;
using FactoryReport.Domain.Import;
using FactoryReport.Domain.Planning;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.Infrastructure.Import;

/// <summary>
/// Fake 导入工作区：内存保存批次/错误/解析行/版本与已发布计划行。
/// 『仅用于开发测试』；不得写入 Oracle 业务表。
/// </summary>
public sealed class FakeImportBatchWorkspace : IImportBatchWorkspace
{
    private readonly ConcurrentDictionary<Guid, ImportBatch> _batches = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<ImportRowError>> _errors = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<IReadOnlyDictionary<string, string?>>> _rows = new();
    private readonly ConcurrentDictionary<Guid, DatasetVersionState> _versions = new();
    private readonly ConcurrentDictionary<Guid, (Guid PublishedVersionId, Guid? PreviousActiveVersionId)> _links = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<DailyProductionPlanLine>> _planLines = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyList<IReadOnlyDictionary<string, string?>>> _actualRows = new();

    public FakeImportBatchWorkspace(FakeFixtureSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var batch in snapshot.ImportBatches)
        {
            _batches[batch.Id] = batch;
            _errors[batch.Id] = [];
            _rows[batch.Id] = [];
        }

        foreach (var version in snapshot.DatasetVersions)
        {
            _versions[version.Id] = version;
        }
    }

    public Task<ImportBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _batches.TryGetValue(batchId, out var batch);
        return Task.FromResult(batch);
    }

    public Task<IReadOnlyList<ImportBatch>> ListBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = _batches.Values.Where(b => b.FactoryId == factoryId);
        if (!string.IsNullOrWhiteSpace(datasetCode))
        {
            var code = datasetCode.Trim();
            query = query.Where(b => string.Equals(b.DatasetCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<ImportBatch> list = query.ToList();
        return Task.FromResult(list);
    }

    public Task SaveBatchAsync(ImportBatch batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        _batches[batch.Id] = batch;
        return Task.CompletedTask;
    }

    public Task ReplaceErrorsAsync(
        Guid batchId,
        IReadOnlyList<ImportRowError> errors,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(errors);
        cancellationToken.ThrowIfCancellationRequested();
        _errors[batchId] = errors.ToArray();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ImportRowError>> GetErrorsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_errors.TryGetValue(batchId, out var errors))
        {
            return Task.FromResult(errors);
        }

        IReadOnlyList<ImportRowError> empty = [];
        return Task.FromResult(empty);
    }

    public Task ReplaceParsedRowsAsync(
        Guid batchId,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        cancellationToken.ThrowIfCancellationRequested();
        _rows[batchId] = rows.ToArray();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> GetParsedRowsAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_rows.TryGetValue(batchId, out var rows))
        {
            return Task.FromResult(rows);
        }

        IReadOnlyList<IReadOnlyDictionary<string, string?>> empty = [];
        return Task.FromResult(empty);
    }

    public Task<IReadOnlyList<DatasetVersionState>> GetDatasetVersionsAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = _versions.Values.Where(v => v.FactoryId == factoryId);
        if (!string.IsNullOrWhiteSpace(datasetCode))
        {
            var code = datasetCode.Trim();
            query = query.Where(v => string.Equals(v.DatasetCode, code, StringComparison.Ordinal));
        }

        IReadOnlyList<DatasetVersionState> list = query.ToList();
        return Task.FromResult(list);
    }

    public Task UpsertDatasetVersionAsync(DatasetVersionState version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        cancellationToken.ThrowIfCancellationRequested();
        _versions[version.Id] = version;
        return Task.CompletedTask;
    }

    public Task SetBatchPublishedVersionAsync(
        Guid batchId,
        Guid datasetVersionId,
        Guid? previousActiveVersionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _links[batchId] = (datasetVersionId, previousActiveVersionId);
        return Task.CompletedTask;
    }

    public Task<(Guid? PublishedVersionId, Guid? PreviousActiveVersionId)> GetBatchVersionLinksAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_links.TryGetValue(batchId, out var link))
        {
            return Task.FromResult<(Guid?, Guid?)>((link.PublishedVersionId, link.PreviousActiveVersionId));
        }

        return Task.FromResult<(Guid?, Guid?)>((null, null));
    }

    public Task ReplacePublishedPlanLinesAsync(
        Guid datasetVersionId,
        IReadOnlyList<DailyProductionPlanLine> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        cancellationToken.ThrowIfCancellationRequested();
        _planLines[datasetVersionId] = lines.ToArray();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DailyProductionPlanLine>> GetPublishedPlanLinesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DailyProductionPlanLine> list = _planLines.Values.SelectMany(x => x).ToList();
        return Task.FromResult(list);
    }

    public Task ReplacePublishedActualRowsAsync(
        Guid datasetVersionId,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        cancellationToken.ThrowIfCancellationRequested();
        _actualRows[datasetVersionId] = rows.ToArray();
        return Task.CompletedTask;
    }
}
