namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 当前用户可读的组织数据范围（数据库无关）。
/// </summary>
public interface IDataScope
{
    /// <summary>
    /// 全局访问（典型：SystemAdmin 的服务端账号配置）。
    /// 不得仅凭角色 Claim 推断。
    /// </summary>
    bool IsGlobal { get; }

    /// <summary>
    /// 显式授权条目。IsGlobal 时可为空。
    /// </summary>
    IReadOnlyList<DataScopeGrant> Grants { get; }

    bool AllowsFactory(long factoryId);

    /// <summary>
    /// 是否允许在该工厂下访问指定车间（请求显式传入时）。
    /// </summary>
    bool AllowsWorkshop(long factoryId, long workshopId);

    /// <summary>
    /// 是否允许在该工厂/车间下访问指定产线（请求显式传入时）。
    /// </summary>
    bool AllowsProductionLine(long factoryId, long workshopId, long productionLineId);

    /// <summary>
    /// 该工厂下被授权的车间 ID 集合；null 表示工厂级全开（或全局）。
    /// </summary>
    IReadOnlySet<long>? GetAuthorizedWorkshopIds(long factoryId);

    /// <summary>
    /// 该工厂下被授权的产线 ID 集合；null 表示在已授权车间内产线全开（或全局/工厂级）。
    /// </summary>
    IReadOnlySet<long>? GetAuthorizedProductionLineIds(long factoryId);
}
