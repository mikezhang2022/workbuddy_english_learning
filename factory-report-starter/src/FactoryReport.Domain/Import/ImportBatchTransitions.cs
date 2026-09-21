namespace FactoryReport.Domain.Import;

/// <summary>
/// <see cref="ImportBatch"/> 状态流转约束（复用既有枚举，不另造平行状态机）。
/// 产品语义：草稿(Pending) → 已校验(Succeeded) / 校验失败(Failed) → 发布由 <see cref="DatasetVersionState"/> 承载。
/// </summary>
public static class ImportBatchTransitions
{
    /// <summary>草稿（已上传、待校验）。</summary>
    public static bool IsDraft(ImportBatchStatus status) => status == ImportBatchStatus.Pending;

    /// <summary>已校验且无错误，可进入发布。</summary>
    public static bool IsReadyToPublish(ImportBatch batch)
        => batch.Status == ImportBatchStatus.Succeeded
           && (batch.ErrorRows ?? 0) == 0
           && (batch.TotalRows ?? 0) > 0
           && batch.TargetPublishStatus is DatasetPublishStatus.Draft or DatasetPublishStatus.Validating;

    /// <summary>已发布（批次侧标记）。</summary>
    public static bool IsPublished(ImportBatch batch)
        => batch.Status == ImportBatchStatus.Succeeded
           && batch.TargetPublishStatus == DatasetPublishStatus.Published;

    public static void EnsureCanValidate(ImportBatch batch)
    {
        if (batch.Status is not (ImportBatchStatus.Pending or ImportBatchStatus.Failed or ImportBatchStatus.Succeeded))
        {
            throw new InvalidOperationException(
                $"Batch status '{batch.Status}' cannot be validated. Allowed: Pending, Failed, Succeeded (re-validate before publish).");
        }

        if (batch.TargetPublishStatus == DatasetPublishStatus.Published)
        {
            throw new InvalidOperationException("Published batch cannot be re-validated.");
        }
    }

    public static void EnsureCanPublish(ImportBatch batch)
    {
        if (!IsReadyToPublish(batch))
        {
            throw new InvalidOperationException(
                "Batch cannot be published: require Succeeded status, zero error rows, positive total rows, and non-Published target.");
        }
    }

    public static void EnsureCanRollback(ImportBatch batch)
    {
        if (!IsPublished(batch))
        {
            throw new InvalidOperationException("Only a published batch can be rolled back.");
        }
    }
}
