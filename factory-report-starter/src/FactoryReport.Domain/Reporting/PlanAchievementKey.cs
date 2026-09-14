namespace FactoryReport.Domain.Reporting;

/// <summary>
/// 计划达成关联键（已确认）：FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode。
/// </summary>
public sealed class PlanAchievementKey : IEquatable<PlanAchievementKey>
{
    public long FactoryId { get; }
    public long WorkshopId { get; }
    public long ProductionLineId { get; }
    public DateOnly ProductionDate { get; }
    public string ProductCode { get; }

    public PlanAchievementKey(
        long factoryId,
        long workshopId,
        long productionLineId,
        DateOnly productionDate,
        string productCode)
    {
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        if (workshopId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workshopId), "WorkshopId must be a positive identifier.");
        }

        if (productionLineId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productionLineId), "ProductionLineId must be a positive identifier.");
        }

        if (productionDate == default)
        {
            throw new ArgumentException("ProductionDate is required.", nameof(productionDate));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        FactoryId = factoryId;
        WorkshopId = workshopId;
        ProductionLineId = productionLineId;
        ProductionDate = productionDate;
        ProductCode = productCode.Trim();
    }

    public bool Equals(PlanAchievementKey? other)
    {
        if (other is null)
        {
            return false;
        }

        return FactoryId == other.FactoryId
            && WorkshopId == other.WorkshopId
            && ProductionLineId == other.ProductionLineId
            && ProductionDate == other.ProductionDate
            && string.Equals(ProductCode, other.ProductCode, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as PlanAchievementKey);

    public override int GetHashCode() =>
        HashCode.Combine(FactoryId, WorkshopId, ProductionLineId, ProductionDate, ProductCode);
}
