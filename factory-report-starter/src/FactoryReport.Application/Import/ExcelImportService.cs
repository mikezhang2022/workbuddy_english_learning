using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Security.DataScope;
using FactoryReport.Domain.Import;
using FactoryReport.Domain.Planning;

namespace FactoryReport.Application.Import;

public interface IExcelImportService
{
    IReadOnlyList<ImportTemplateDefinition> ListTemplates();

    byte[] DownloadTemplate(string datasetCode);

    Task<ImportBatchDetailDto> UploadAsync(
        long factoryId,
        string datasetCode,
        string fileName,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<ImportValidationReportDto> ValidateAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportBatchSummaryDto>> ListBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default);

    Task<ImportBatchDetailDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<ImportPublishResultDto> PublishAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<ImportRollbackResultDto> RollbackAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public sealed class ExcelImportOptions
{
    public const long MaxFileBytes = 20 * 1024 * 1024;
    public const int MaxDataRows = 50_000;
    public static readonly string[] AllowedExtensions = [".xlsx"];
    public static readonly string[] AllowedContentTypes =
    [
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/octet-stream",
        "application/zip"
    ];
}

public sealed class ExcelImportService(
    IExcelWorkbookService workbookService,
    IImportBatchWorkspace workspace,
    IReportDataQueryService queryService,
    IReportQueryScopeService scopeService,
    IUtcClock clock) : IExcelImportService
{
    public IReadOnlyList<ImportTemplateDefinition> ListTemplates() => ImportTemplateCatalog.All;

    public byte[] DownloadTemplate(string datasetCode)
    {
        EnsureKnownDataset(datasetCode);
        return workbookService.CreateTemplate(ImportTemplateCatalog.GetRequired(datasetCode.Trim()));
    }

    public async Task<ImportBatchDetailDto> UploadAsync(
        long factoryId,
        string datasetCode,
        string fileName,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        EnsureKnownDataset(datasetCode);
        var code = datasetCode.Trim();

        await scopeService.ResolveEffectiveOrganizationScopeAsync(factoryId, null, null, cancellationToken)
            .ConfigureAwait(false);

        ValidateUploadFile(fileName, contentLength);

        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows;
        try
        {
            var template = ImportTemplateCatalog.GetRequired(code);
            rows = workbookService.ParseDataRows(
                content,
                template.SheetName,
                template.Columns.Select(c => c.Name).ToArray());
        }
        catch (ExcelParseException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ExcelParseException("无法解析 Excel 文件。请确认文件为有效的 .xlsx 且使用官方模板表头。");
        }

        if (rows.Count > ExcelImportOptions.MaxDataRows)
        {
            throw ReportQueryValidationException.ForField(
                "file",
                $"数据行数超过上限 {ExcelImportOptions.MaxDataRows}。");
        }

        var now = clock.UtcNow;
        var batch = new ImportBatch(
            id: Guid.NewGuid(),
            factoryId: factoryId,
            datasetCode: code,
            status: ImportBatchStatus.Pending,
            createdAtUtc: now,
            targetPublishStatus: DatasetPublishStatus.Draft,
            sourceFileName: SanitizeFileName(fileName),
            totalRows: rows.Count,
            errorRows: null,
            completedAtUtc: null);

        await workspace.SaveBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        await workspace.ReplaceParsedRowsAsync(batch.Id, rows, cancellationToken).ConfigureAwait(false);
        await workspace.ReplaceErrorsAsync(batch.Id, [], cancellationToken).ConfigureAwait(false);

        return await GetBatchAsync(batch.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportValidationReportDto> ValidateAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        await scopeService.ResolveEffectiveOrganizationScopeAsync(batch.FactoryId, null, null, cancellationToken)
            .ConfigureAwait(false);
        ImportBatchTransitions.EnsureCanValidate(batch);

        var validating = batch.WithStatus(ImportBatchStatus.Validating);
        await workspace.SaveBatchAsync(validating, cancellationToken).ConfigureAwait(false);

        var rows = await workspace.GetParsedRowsAsync(batchId, cancellationToken).ConfigureAwait(false);
        var factories = await queryService.GetFactoriesAsync(cancellationToken).ConfigureAwait(false);
        var workshops = await queryService.GetWorkshopsAsync(batch.FactoryId, cancellationToken).ConfigureAwait(false);
        var lines = await queryService.GetProductionLinesAsync(batch.FactoryId, null, cancellationToken)
            .ConfigureAwait(false);
        var products = await queryService.GetProductsAsync(batch.FactoryId, null, cancellationToken)
            .ConfigureAwait(false);

        // 组织编码须与批次工厂一致：限制工厂列表
        var factoryScoped = factories.Where(f => f.Id == batch.FactoryId).ToList();
        var errors = ImportRowValidator.Validate(
            batch.DatasetCode,
            rows,
            factoryScoped,
            workshops,
            lines,
            products);

        // 行内工厂编码必须映射到当前批次工厂
        foreach (var err in errors.Where(e => e.Code == ImportRowValidator.CodeOrgNotFound).ToList())
        {
            _ = err;
        }

        var now = clock.UtcNow;
        var distinctErrors = DistinctErrorRows(errors);
        var status = errors.Count == 0 ? ImportBatchStatus.Succeeded : ImportBatchStatus.Failed;
        var completed = validating.WithStatus(
            status,
            targetPublishStatus: DatasetPublishStatus.Draft,
            totalRows: rows.Count,
            errorRows: distinctErrors,
            completedAtUtc: now);

        await workspace.ReplaceErrorsAsync(batchId, errors, cancellationToken).ConfigureAwait(false);
        await workspace.SaveBatchAsync(completed, cancellationToken).ConfigureAwait(false);

        return new ImportValidationReportDto
        {
            BatchId = batchId,
            Status = completed.Status.ToString(),
            TotalRows = rows.Count,
            SuccessRows = Math.Max(0, rows.Count - distinctErrors),
            ErrorRows = distinctErrors,
            CanPublish = ImportBatchTransitions.IsReadyToPublish(completed),
            Errors = errors.Select(ImportBatchDtoMapper.ToDto).ToArray()
        };
    }

    public async Task<IReadOnlyList<ImportBatchSummaryDto>> ListBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        await scopeService.ResolveEffectiveOrganizationScopeAsync(factoryId, null, null, cancellationToken)
            .ConfigureAwait(false);

        var batches = await workspace.ListBatchesAsync(factoryId, datasetCode, cancellationToken)
            .ConfigureAwait(false);
        var result = new List<ImportBatchSummaryDto>(batches.Count);
        foreach (var batch in batches.OrderByDescending(b => b.CreatedAtUtc.Value))
        {
            result.Add(await ToSummaryAsync(batch, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    public async Task<ImportBatchDetailDto> GetBatchAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        await scopeService.ResolveEffectiveOrganizationScopeAsync(batch.FactoryId, null, null, cancellationToken)
            .ConfigureAwait(false);

        var summary = await ToSummaryAsync(batch, cancellationToken).ConfigureAwait(false);
        var errors = await workspace.GetErrorsAsync(batchId, cancellationToken).ConfigureAwait(false);
        var (publishedVersionId, _) = await workspace.GetBatchVersionLinksAsync(batchId, cancellationToken)
            .ConfigureAwait(false);

        string? publishedVersionNo = null;
        string? activeVersionNo = null;
        if (publishedVersionId is Guid vid)
        {
            var versions = await workspace.GetDatasetVersionsAsync(batch.FactoryId, batch.DatasetCode, cancellationToken)
                .ConfigureAwait(false);
            publishedVersionNo = versions.FirstOrDefault(v => v.Id == vid)?.VersionNo;
            activeVersionNo = versions.FirstOrDefault(v => v.IsActive)?.VersionNo;
        }

        return new ImportBatchDetailDto
        {
            BatchId = summary.BatchId,
            FactoryId = summary.FactoryId,
            DatasetCode = summary.DatasetCode,
            DatasetDisplayName = summary.DatasetDisplayName,
            Status = summary.Status,
            LifecycleLabel = summary.LifecycleLabel,
            TargetPublishStatus = summary.TargetPublishStatus,
            SourceFileName = summary.SourceFileName,
            TotalRows = summary.TotalRows,
            SuccessRows = summary.SuccessRows,
            ErrorRows = summary.ErrorRows,
            CreatedAtUtc = summary.CreatedAtUtc,
            CompletedAtUtc = summary.CompletedAtUtc,
            PublishedVersionId = summary.PublishedVersionId,
            CanPublish = summary.CanPublish,
            CanRollback = summary.CanRollback,
            Errors = errors.Select(ImportBatchDtoMapper.ToDto).ToArray(),
            PublishedVersionNo = publishedVersionNo,
            ActiveVersionNo = activeVersionNo
        };
    }

    public async Task<ImportPublishResultDto> PublishAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        await scopeService.ResolveEffectiveOrganizationScopeAsync(batch.FactoryId, null, null, cancellationToken)
            .ConfigureAwait(false);
        ImportBatchTransitions.EnsureCanPublish(batch);

        var errors = await workspace.GetErrorsAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("存在校验错误时不得发布。");
        }

        var now = clock.UtcNow;
        var versionId = Guid.NewGuid();
        var versionNo = $"v{now.Value:yyyy.MM.dd.HHmmss}-{batch.FactoryId:D}-{versionId.ToString("N")[..8]}";

        var versions = await workspace
            .GetDatasetVersionsAsync(batch.FactoryId, batch.DatasetCode, cancellationToken)
            .ConfigureAwait(false);
        var previousActive = versions.FirstOrDefault(v => v.IsActive);
        if (previousActive is not null)
        {
            await workspace.UpsertDatasetVersionAsync(
                    DatasetVersionTransitions.Deactivate(previousActive),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var published = DatasetVersionTransitions.PublishAndActivate(
            DatasetVersionTransitions.CreateDraft(
                versionId,
                batch.FactoryId,
                batch.DatasetCode,
                versionNo,
                now),
            now);
        await workspace.UpsertDatasetVersionAsync(published, cancellationToken).ConfigureAwait(false);

        var rows = await workspace.GetParsedRowsAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (string.Equals(batch.DatasetCode, ImportDatasetCodes.PlanData, StringComparison.Ordinal))
        {
            var planLines = await BuildPlanLinesAsync(batch, rows, versionId, cancellationToken)
                .ConfigureAwait(false);
            await workspace.ReplacePublishedPlanLinesAsync(versionId, planLines, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await workspace.ReplacePublishedActualRowsAsync(versionId, rows, cancellationToken)
                .ConfigureAwait(false);
        }

        await workspace.SetBatchPublishedVersionAsync(
                batchId,
                versionId,
                previousActive?.Id,
                cancellationToken)
            .ConfigureAwait(false);

        var publishedBatch = batch.WithStatus(
            ImportBatchStatus.Succeeded,
            targetPublishStatus: DatasetPublishStatus.Published,
            completedAtUtc: now);
        await workspace.SaveBatchAsync(publishedBatch, cancellationToken).ConfigureAwait(false);

        return new ImportPublishResultDto
        {
            BatchId = batchId,
            DatasetVersionId = versionId,
            VersionNo = versionNo,
            Status = nameof(DatasetPublishStatus.Published),
            IsActive = true
        };
    }

    public async Task<ImportRollbackResultDto> RollbackAsync(
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batch = await RequireBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        await scopeService.ResolveEffectiveOrganizationScopeAsync(batch.FactoryId, null, null, cancellationToken)
            .ConfigureAwait(false);
        ImportBatchTransitions.EnsureCanRollback(batch);

        var (publishedVersionId, previousActiveVersionId) = await workspace
            .GetBatchVersionLinksAsync(batchId, cancellationToken)
            .ConfigureAwait(false);

        if (publishedVersionId is null)
        {
            throw new InvalidOperationException("Batch has no published version to roll back.");
        }

        var versions = await workspace
            .GetDatasetVersionsAsync(batch.FactoryId, batch.DatasetCode, cancellationToken)
            .ConfigureAwait(false);
        var current = versions.FirstOrDefault(v => v.Id == publishedVersionId.Value)
            ?? throw new InvalidOperationException("Published version was not found.");

        await workspace.UpsertDatasetVersionAsync(
                DatasetVersionTransitions.Disable(current),
                cancellationToken)
            .ConfigureAwait(false);

        Guid? reactivatedId = null;
        string? reactivatedNo = null;
        if (previousActiveVersionId is Guid prevId)
        {
            var previous = versions.FirstOrDefault(v => v.Id == prevId);
            if (previous is not null
                && previous.PublishStatus == DatasetPublishStatus.Published
                && previous.PublishedAtUtc is not null)
            {
                var reactivated = DatasetVersionTransitions.Reactivate(
                    previous.IsActive ? DatasetVersionTransitions.Deactivate(previous) : previous);
                await workspace.UpsertDatasetVersionAsync(reactivated, cancellationToken)
                    .ConfigureAwait(false);
                reactivatedId = reactivated.Id;
                reactivatedNo = reactivated.VersionNo;
            }
        }

        var rolled = batch.WithStatus(
            ImportBatchStatus.Succeeded,
            targetPublishStatus: DatasetPublishStatus.Disabled,
            completedAtUtc: clock.UtcNow);
        await workspace.SaveBatchAsync(rolled, cancellationToken).ConfigureAwait(false);
        await workspace.SetBatchPublishedVersionAsync(
                batchId,
                publishedVersionId.Value,
                previousActiveVersionId,
                cancellationToken)
            .ConfigureAwait(false);

        return new ImportRollbackResultDto
        {
            BatchId = batchId,
            DisabledVersionId = publishedVersionId.Value,
            ReactivatedVersionId = reactivatedId,
            ReactivatedVersionNo = reactivatedNo
        };
    }

    private async Task<IReadOnlyList<DailyProductionPlanLine>> BuildPlanLinesAsync(
        ImportBatch batch,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var workshops = await queryService.GetWorkshopsAsync(batch.FactoryId, cancellationToken)
            .ConfigureAwait(false);
        var lines = await queryService.GetProductionLinesAsync(batch.FactoryId, null, cancellationToken)
            .ConfigureAwait(false);
        var result = new List<DailyProductionPlanLine>(rows.Count);

        foreach (var row in rows)
        {
            var workshopCode = row["workshopCode"]!.Trim();
            var workshop = workshops.First(w =>
                string.Equals(w.Code, workshopCode, StringComparison.OrdinalIgnoreCase));
            long? lineId = null;
            if (row.TryGetValue("productionLineCode", out var lineCode)
                && !string.IsNullOrWhiteSpace(lineCode))
            {
                lineId = lines.First(l =>
                    string.Equals(l.Code, lineCode.Trim(), StringComparison.OrdinalIgnoreCase)).Id;
            }

            ImportRowValidator.TryParseDate(row["productionDate"]!.Trim(), out var planDate);
            ImportRowValidator.TryParseDecimal(row["planQuantity"]!.Trim(), out var qty);
            row.TryGetValue("remark", out var remark);

            result.Add(new DailyProductionPlanLine(
                factoryId: batch.FactoryId,
                workshopId: workshop.Id,
                planDate: planDate,
                productCode: row["productCode"]!.Trim(),
                planQuantity: qty,
                planVersionId: versionId,
                productionLineId: lineId,
                remark: string.IsNullOrWhiteSpace(remark) ? null : remark.Trim()));
        }

        return result;
    }

    private async Task<ImportBatchSummaryDto> ToSummaryAsync(
        ImportBatch batch,
        CancellationToken cancellationToken)
    {
        var (publishedVersionId, _) = await workspace.GetBatchVersionLinksAsync(batch.Id, cancellationToken)
            .ConfigureAwait(false);
        var errorRows = batch.ErrorRows;
        if (errorRows is null)
        {
            var errors = await workspace.GetErrorsAsync(batch.Id, cancellationToken).ConfigureAwait(false);
            errorRows = DistinctErrorRows(errors);
        }

        return new ImportBatchSummaryDto
        {
            BatchId = batch.Id,
            FactoryId = batch.FactoryId,
            DatasetCode = batch.DatasetCode,
            DatasetDisplayName = ImportDatasetCodes.DisplayName(batch.DatasetCode),
            Status = batch.Status.ToString(),
            LifecycleLabel = ImportBatchDtoMapper.ToLifecycleLabel(batch),
            TargetPublishStatus = batch.TargetPublishStatus.ToString(),
            SourceFileName = batch.SourceFileName,
            TotalRows = batch.TotalRows,
            SuccessRows = batch.TotalRows is int t && errorRows is int e ? Math.Max(0, t - e) : batch.SuccessRows,
            ErrorRows = errorRows,
            CreatedAtUtc = batch.CreatedAtUtc.Value,
            CompletedAtUtc = batch.CompletedAtUtc?.Value,
            PublishedVersionId = publishedVersionId,
            CanPublish = ImportBatchTransitions.IsReadyToPublish(batch),
            CanRollback = ImportBatchTransitions.IsPublished(batch)
        };
    }

    private async Task<ImportBatch> RequireBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await workspace.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            throw ReportQueryValidationException.ForField("batchId", $"导入批次不存在：{batchId}。");
        }

        return batch;
    }

    private static void EnsureKnownDataset(string datasetCode)
    {
        if (!ImportDatasetCodes.IsKnown(datasetCode))
        {
            throw ReportQueryValidationException.ForField(
                "datasetCode",
                $"不支持的数据集编码「{datasetCode}」。允许：{string.Join(", ", ImportDatasetCodes.All)}。");
        }
    }

    private static void ValidateUploadFile(string fileName, long contentLength)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw ReportQueryValidationException.ForField("file", "文件名不能为空。");
        }

        var ext = Path.GetExtension(fileName);
        if (!ExcelImportOptions.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            throw ReportQueryValidationException.ForField(
                "file",
                "仅支持 .xlsx 文件（不支持 .xlsm / 宏 / csv 本阶段上传）。");
        }

        if (contentLength <= 0)
        {
            throw ReportQueryValidationException.ForField("file", "文件内容为空。");
        }

        if (contentLength > ExcelImportOptions.MaxFileBytes)
        {
            throw ReportQueryValidationException.ForField(
                "file",
                $"文件大小超过上限 {ExcelImportOptions.MaxFileBytes / (1024 * 1024)} MB。");
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        return string.IsNullOrWhiteSpace(name) ? "upload.xlsx" : name;
    }

    private static int DistinctErrorRows(IReadOnlyList<ImportRowError> errors)
        => errors.Select(e => e.RowNumber).Distinct().Count();
}
