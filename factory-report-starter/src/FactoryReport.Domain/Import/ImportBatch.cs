using FactoryReport.Domain.Common;

namespace FactoryReport.Domain.Import;

/// <summary>
/// 导入批次状态（数据导入追踪）。
/// </summary>
public enum ImportBatchStatus
{
    Pending = 0,
    Validating = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4
}

/// <summary>
/// 数据集/数据版本发布状态。与报表生命周期 Draft → Validating → Published → Disabled 对齐。
/// </summary>
public enum DatasetPublishStatus
{
    Draft = 0,
    Validating = 1,
    Published = 2,
    Disabled = 3
}

/// <summary>
/// 导入批次追踪。不包含物理文件存储或 Oracle 映射。
/// </summary>
public sealed class ImportBatch
{
    public Guid Id { get; }
    public long FactoryId { get; }
    public string DatasetCode { get; }
    public ImportBatchStatus Status { get; }
    public DatasetPublishStatus TargetPublishStatus { get; }
    public string? SourceFileName { get; }
    public int? TotalRows { get; }
    public int? ErrorRows { get; }
    public UtcInstant CreatedAtUtc { get; }
    public UtcInstant? CompletedAtUtc { get; }

    public ImportBatch(
        Guid id,
        long factoryId,
        string datasetCode,
        ImportBatchStatus status,
        UtcInstant createdAtUtc,
        DatasetPublishStatus targetPublishStatus = DatasetPublishStatus.Draft,
        string? sourceFileName = null,
        int? totalRows = null,
        int? errorRows = null,
        UtcInstant? completedAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("ImportBatch Id must be a non-empty GUID.", nameof(id));
        }

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(datasetCode);

        if (totalRows is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRows), "TotalRows must not be negative.");
        }

        if (errorRows is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(errorRows), "ErrorRows must not be negative.");
        }

        Id = id;
        FactoryId = factoryId;
        DatasetCode = datasetCode.Trim();
        Status = status;
        TargetPublishStatus = targetPublishStatus;
        SourceFileName = string.IsNullOrWhiteSpace(sourceFileName) ? null : sourceFileName.Trim();
        TotalRows = totalRows;
        ErrorRows = errorRows;
        CreatedAtUtc = createdAtUtc;
        CompletedAtUtc = completedAtUtc;
    }
}

/// <summary>
/// 数据版本领域状态：用于 Active 版本切换与不可变发布追踪。
/// </summary>
public sealed class DatasetVersionState
{
    public Guid Id { get; }
    public long FactoryId { get; }
    public string DatasetCode { get; }
    public string VersionNo { get; }
    public DatasetPublishStatus PublishStatus { get; }
    public bool IsActive { get; }
    public UtcInstant CreatedAtUtc { get; }
    public UtcInstant? PublishedAtUtc { get; }

    public DatasetVersionState(
        Guid id,
        long factoryId,
        string datasetCode,
        string versionNo,
        DatasetPublishStatus publishStatus,
        bool isActive,
        UtcInstant createdAtUtc,
        UtcInstant? publishedAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("DatasetVersion Id must be a non-empty GUID.", nameof(id));
        }

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(datasetCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionNo);

        if (isActive && publishStatus != DatasetPublishStatus.Published)
        {
            throw new ArgumentException("Only a Published version may be marked Active.", nameof(isActive));
        }

        if (publishStatus == DatasetPublishStatus.Published && publishedAtUtc is null)
        {
            throw new ArgumentException("PublishedAtUtc is required when PublishStatus is Published.", nameof(publishedAtUtc));
        }

        Id = id;
        FactoryId = factoryId;
        DatasetCode = datasetCode.Trim();
        VersionNo = versionNo.Trim();
        PublishStatus = publishStatus;
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
        PublishedAtUtc = publishedAtUtc;
    }
}
