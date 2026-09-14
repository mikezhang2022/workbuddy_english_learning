namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 组织范围筛选：必须指定 FactoryId；可选车间/产线，防止跨工厂串扰。
/// </summary>
public sealed class OrganizationScopeFilter
{
    public long FactoryId { get; }
    public long? WorkshopId { get; }
    public long? ProductionLineId { get; }

    public OrganizationScopeFilter(long factoryId, long? workshopId = null, long? productionLineId = null)
    {
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

        FactoryId = factoryId;
        WorkshopId = workshopId;
        ProductionLineId = productionLineId;
    }
}
