using FactoryReport.Domain.Common;
using FactoryReport.Domain.Production;

namespace FactoryReport.Domain.MasterData;

/// <summary>
/// 工单主数据标识。延期等正式口径【待现场确认】，本对象仅保存标识与分列数量。
/// <para>
/// StatusCode 为临时字符串（如 Open / Completed / Closed），
/// 『仅用于开发测试，不代表现场 MES 正式枚举』；正式状态枚举【待现场确认】。
/// </para>
/// </summary>
public sealed class WorkOrder
{
    public long Id { get; }
    public long FactoryId { get; }
    public long? WorkshopId { get; }
    public long? ProductionLineId { get; }
    public string WorkOrderNo { get; }
    public string ProductCode { get; }
    public decimal PlanQuantity { get; }
    public decimal CompletedQuantity { get; }
    public UtcInstant? PlannedStartAtUtc { get; }
    public UtcInstant? PlannedFinishAtUtc { get; }
    public UtcInstant? ActualFinishAtUtc { get; }
    public string? StatusCode { get; }

    public WorkOrder(
        long id,
        long factoryId,
        string workOrderNo,
        string productCode,
        decimal planQuantity,
        decimal completedQuantity,
        long? workshopId = null,
        long? productionLineId = null,
        UtcInstant? plannedStartAtUtc = null,
        UtcInstant? plannedFinishAtUtc = null,
        UtcInstant? actualFinishAtUtc = null,
        string? statusCode = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "WorkOrder Id must be a positive identifier.");
        }

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        if (workshopId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workshopId), "WorkshopId must be positive when provided.");
        }

        if (productionLineId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productionLineId), "ProductionLineId must be positive when provided.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(workOrderNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        QuantityGuard.EnsureNonNegative(planQuantity, nameof(planQuantity));
        QuantityGuard.EnsureNonNegative(completedQuantity, nameof(completedQuantity));

        Id = id;
        FactoryId = factoryId;
        WorkshopId = workshopId;
        ProductionLineId = productionLineId;
        WorkOrderNo = workOrderNo.Trim();
        ProductCode = productCode.Trim();
        PlanQuantity = planQuantity;
        CompletedQuantity = completedQuantity;
        PlannedStartAtUtc = plannedStartAtUtc;
        PlannedFinishAtUtc = plannedFinishAtUtc;
        ActualFinishAtUtc = actualFinishAtUtc;
        StatusCode = string.IsNullOrWhiteSpace(statusCode) ? null : statusCode.Trim();
    }
}
