namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 单条组织数据范围授权。
/// <list type="bullet">
/// <item><description>仅 FactoryId：该工厂内全部车间/产线（工厂级授权）。</description></item>
/// <item><description>FactoryId + WorkshopId：该车间内全部产线；不继承同级其他车间。</description></item>
/// <item><description>FactoryId + WorkshopId + ProductionLineId：仅该产线。</description></item>
/// </list>
/// 授权来自服务端账号配置，不得由客户端 query / Header / 角色 Claim 自行扩大。
/// </summary>
public sealed class DataScopeGrant
{
    public long FactoryId { get; }
    public long? WorkshopId { get; }
    public long? ProductionLineId { get; }

    public DataScopeGrant(long factoryId, long? workshopId = null, long? productionLineId = null)
    {
        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be positive.");
        }

        if (workshopId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workshopId), "WorkshopId must be positive when provided.");
        }

        if (productionLineId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(productionLineId), "ProductionLineId must be positive when provided.");
        }

        if (productionLineId is not null && workshopId is null)
        {
            throw new ArgumentException("ProductionLineId requires WorkshopId.", nameof(productionLineId));
        }

        FactoryId = factoryId;
        WorkshopId = workshopId;
        ProductionLineId = productionLineId;
    }

    public bool IsFactoryWide => WorkshopId is null && ProductionLineId is null;

    public bool IsWorkshopWide => WorkshopId is not null && ProductionLineId is null;
}
