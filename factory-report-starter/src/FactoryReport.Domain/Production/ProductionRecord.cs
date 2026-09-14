using FactoryReport.Domain.Common;

namespace FactoryReport.Domain.Production;

/// <summary>
/// 生产事实记录。数量分列保存在 <see cref="ProductionQuantities"/>；
/// 夜班归属、良率/返工/报废正式口径【待现场确认】，不在本对象内伪装为正式规则。
/// </summary>
public sealed class ProductionRecord
{
    public long FactoryId { get; }
    public long WorkshopId { get; }
    public long ProductionLineId { get; }
    public DateOnly ProductionDate { get; }
    public string ProductCode { get; }
    public string? ShiftCode { get; }
    public ProductionQuantities Quantities { get; }
    public UtcInstant DataUpdatedAtUtc { get; }

    public ProductionRecord(
        long factoryId,
        long workshopId,
        long productionLineId,
        DateOnly productionDate,
        string productCode,
        ProductionQuantities quantities,
        UtcInstant dataUpdatedAtUtc,
        string? shiftCode = null)
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
        ArgumentNullException.ThrowIfNull(quantities);

        FactoryId = factoryId;
        WorkshopId = workshopId;
        ProductionLineId = productionLineId;
        ProductionDate = productionDate;
        ProductCode = productCode.Trim();
        Quantities = quantities;
        DataUpdatedAtUtc = dataUpdatedAtUtc;
        ShiftCode = string.IsNullOrWhiteSpace(shiftCode) ? null : shiftCode.Trim();
    }
}
