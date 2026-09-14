using FactoryReport.Domain.Production;

namespace FactoryReport.Domain.Planning;

/// <summary>
/// 月度生产计划日行。必须保留 <see cref="PlanDate"/>（业务计划日，非系统时间）。
/// </summary>
public sealed class DailyProductionPlanLine
{
    public long FactoryId { get; }
    public long WorkshopId { get; }
    public long? ProductionLineId { get; }
    public DateOnly PlanDate { get; }
    public string ProductCode { get; }
    public decimal PlanQuantity { get; }
    public string? Remark { get; }

    public DailyProductionPlanLine(
        long factoryId,
        long workshopId,
        DateOnly planDate,
        string productCode,
        decimal planQuantity,
        long? productionLineId = null,
        string? remark = null)
    {
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        if (workshopId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workshopId), "WorkshopId must be a positive identifier.");
        }

        if (productionLineId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productionLineId), "ProductionLineId must be positive when provided.");
        }

        if (planDate == default)
        {
            throw new ArgumentException("PlanDate is required and must be a valid calendar date.", nameof(planDate));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        QuantityGuard.EnsureNonNegative(planQuantity, nameof(planQuantity));

        FactoryId = factoryId;
        WorkshopId = workshopId;
        ProductionLineId = productionLineId;
        PlanDate = planDate;
        ProductCode = productCode.Trim();
        PlanQuantity = planQuantity;
        Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
    }
}
