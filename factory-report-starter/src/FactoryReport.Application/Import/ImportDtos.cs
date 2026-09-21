using FactoryReport.Domain.Import;

namespace FactoryReport.Application.Import;

public class ImportBatchSummaryDto
{
    public required Guid BatchId { get; init; }
    public required long FactoryId { get; init; }
    public required string DatasetCode { get; init; }
    public required string DatasetDisplayName { get; init; }
    public required string Status { get; init; }
    public required string LifecycleLabel { get; init; }
    public required string TargetPublishStatus { get; init; }
    public string? SourceFileName { get; init; }
    public int? TotalRows { get; init; }
    public int? SuccessRows { get; init; }
    public int? ErrorRows { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public Guid? PublishedVersionId { get; init; }
    public bool CanPublish { get; init; }
    public bool CanRollback { get; init; }
}

public sealed class ImportBatchDetailDto : ImportBatchSummaryDto
{
    public required IReadOnlyList<ImportRowErrorDto> Errors { get; init; }
    public string? ActiveVersionNo { get; init; }
    public string? PublishedVersionNo { get; init; }
}

public sealed class ImportRowErrorDto
{
    public required int RowNumber { get; init; }
    public required string ColumnName { get; init; }
    public string? RawValue { get; init; }
    public required string Reason { get; init; }
    public required string Code { get; init; }
}

public sealed class ImportValidationReportDto
{
    public required Guid BatchId { get; init; }
    public required string Status { get; init; }
    public required int TotalRows { get; init; }
    public required int SuccessRows { get; init; }
    public required int ErrorRows { get; init; }
    public required bool CanPublish { get; init; }
    public required IReadOnlyList<ImportRowErrorDto> Errors { get; init; }
}

public sealed class ImportPublishResultDto
{
    public required Guid BatchId { get; init; }
    public required Guid DatasetVersionId { get; init; }
    public required string VersionNo { get; init; }
    public required string Status { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class ImportRollbackResultDto
{
    public required Guid BatchId { get; init; }
    public required Guid DisabledVersionId { get; init; }
    public Guid? ReactivatedVersionId { get; init; }
    public string? ReactivatedVersionNo { get; init; }
}

public static class ImportBatchDtoMapper
{
    public static string ToLifecycleLabel(ImportBatch batch)
    {
        if (ImportBatchTransitions.IsPublished(batch))
        {
            return "已发布";
        }

        return batch.Status switch
        {
            ImportBatchStatus.Pending => "草稿",
            ImportBatchStatus.Validating => "校验中",
            ImportBatchStatus.Succeeded => "已校验",
            ImportBatchStatus.Failed => "校验失败",
            ImportBatchStatus.Cancelled => "已取消",
            _ => batch.Status.ToString()
        };
    }

    public static ImportRowErrorDto ToDto(ImportRowError error) => new()
    {
        RowNumber = error.RowNumber,
        ColumnName = error.ColumnName,
        RawValue = error.RawValue,
        Reason = error.Reason,
        Code = error.Code
    };
}
