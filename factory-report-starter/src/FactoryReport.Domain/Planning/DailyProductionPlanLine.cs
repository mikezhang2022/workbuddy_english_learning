using FactoryReport.Domain.Production;

namespace FactoryReport.Domain.Planning;

/// <summary>
/// 月度生产计划日行。必须保留 <see cref="PlanDate"/>（业务计划日，非系统时间）。
/// <see cref="PlanVersionId"/> 关联数据集版本；正式发布/激活规则【待现场确认】。
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

    /// <summary>
    /// 所属计划数据版本 Id（对应 <c>DatasetVersionState.Id</c>）。
    /// Fake 层用其过滤 Published/Active；正式 Excel 发布链路【待现场确认】。
    /// </summary>
    public Guid PlanVersionId { get; }

    public DailyProductionPlanLine(
        long factoryId,
        long workshopId,
        DateOnly planDate,
        string productCode,
        decimal planQuantity,
        Guid planVersionId,
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

        if (planVersionId == Guid.Empty)
        {
            throw new ArgumentException("PlanVersionId must be a non-empty GUID.", nameof(planVersionId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        QuantityGuard.EnsureNonNegative(planQuantity, nameof(planQuantity));

        FactoryId = factoryId;
        WorkshopId = workshopId;
        ProductionLineId = productionLineId;
        PlanDate = planDate;
        ProductCode = productCode.Trim();
        PlanQuantity = planQuantity;
        PlanVersionId = planVersionId;
        Remark = string.IsNullOrWhiteSpace(remark) ? null : remark.Trim();
    }
}
