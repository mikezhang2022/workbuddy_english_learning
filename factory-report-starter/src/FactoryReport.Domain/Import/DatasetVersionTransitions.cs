using FactoryReport.Domain.Common;

namespace FactoryReport.Domain.Import;

/// <summary>
/// <see cref="DatasetVersionState"/> 发布 / 激活 / 回退辅助（不可变替换实例）。
/// </summary>
public static class DatasetVersionTransitions
{
    public static DatasetVersionState CreateDraft(
        Guid id,
        long factoryId,
        string datasetCode,
        string versionNo,
        UtcInstant createdAtUtc)
        => new(
            id,
            factoryId,
            datasetCode,
            versionNo,
            DatasetPublishStatus.Draft,
            isActive: false,
            createdAtUtc,
            publishedAtUtc: null);

    public static DatasetVersionState PublishAndActivate(
        DatasetVersionState draftOrExisting,
        UtcInstant publishedAtUtc)
        => new(
            draftOrExisting.Id,
            draftOrExisting.FactoryId,
            draftOrExisting.DatasetCode,
            draftOrExisting.VersionNo,
            DatasetPublishStatus.Published,
            isActive: true,
            draftOrExisting.CreatedAtUtc,
            publishedAtUtc);

    /// <summary>取消激活但保持 Published（被新版本替换的历史 Active）。</summary>
    public static DatasetVersionState Deactivate(DatasetVersionState publishedActive)
    {
        if (publishedActive.PublishStatus != DatasetPublishStatus.Published)
        {
            throw new InvalidOperationException("Only Published versions can be deactivated.");
        }

        return new DatasetVersionState(
            publishedActive.Id,
            publishedActive.FactoryId,
            publishedActive.DatasetCode,
            publishedActive.VersionNo,
            DatasetPublishStatus.Published,
            isActive: false,
            publishedActive.CreatedAtUtc,
            publishedActive.PublishedAtUtc);
    }

    /// <summary>回退：将版本标为 Disabled 且非 Active。</summary>
    public static DatasetVersionState Disable(DatasetVersionState version)
        => new(
            version.Id,
            version.FactoryId,
            version.DatasetCode,
            version.VersionNo,
            DatasetPublishStatus.Disabled,
            isActive: false,
            version.CreatedAtUtc,
            version.PublishedAtUtc);

    /// <summary>回退时重新激活先前 Published 版本。</summary>
    public static DatasetVersionState Reactivate(DatasetVersionState publishedInactive)
    {
        if (publishedInactive.PublishStatus != DatasetPublishStatus.Published)
        {
            throw new InvalidOperationException("Only a Published inactive version can be reactivated.");
        }

        if (publishedInactive.PublishedAtUtc is null)
        {
            throw new InvalidOperationException("PublishedAtUtc is required to reactivate.");
        }

        return new DatasetVersionState(
            publishedInactive.Id,
            publishedInactive.FactoryId,
            publishedInactive.DatasetCode,
            publishedInactive.VersionNo,
            DatasetPublishStatus.Published,
            isActive: true,
            publishedInactive.CreatedAtUtc,
            publishedInactive.PublishedAtUtc);
    }
}
