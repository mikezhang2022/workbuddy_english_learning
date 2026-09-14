namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 组织范围筛选：必须指定 FactoryId；可选车间/产线，防止跨工厂串扰。
/// Authorized* 集合用于授权交集收窄（多车间/多产线授权且请求未指定时）。
/// </summary>
public sealed class OrganizationScopeFilter
{
    public long FactoryId { get; }
    public long? WorkshopId { get; }
    public long? ProductionLineId { get; }

    /// <summary>
    /// 授权交集允许的车间集合；null 表示不额外按集合限制（已用 WorkshopId 或工厂级全开）。
    /// </summary>
    public IReadOnlySet<long>? AuthorizedWorkshopIds { get; }

    /// <summary>
    /// 授权交集允许的产线集合；null 表示不额外按集合限制。
    /// </summary>
    public IReadOnlySet<long>? AuthorizedProductionLineIds { get; }

    public OrganizationScopeFilter(
        long factoryId,
        long? workshopId = null,
        long? productionLineId = null,
        IReadOnlySet<long>? authorizedWorkshopIds = null,
        IReadOnlySet<long>? authorizedProductionLineIds = null)
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
        AuthorizedWorkshopIds = authorizedWorkshopIds;
        AuthorizedProductionLineIds = authorizedProductionLineIds;
    }
}
