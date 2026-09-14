using FactoryReport.Application.DataAccess;

namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 请求筛选范围与用户授权范围的安全交集。
/// 显式请求超出授权 → 拒绝（由调用方映射 403）；不得返回空数据伪装成功。
/// </summary>
public static class DataScopeIntersection
{
    /// <summary>
    /// 将请求组织筛选与用户授权求交，得到可下发到仓储的有效筛选。
    /// </summary>
    /// <exception cref="DataScopeForbiddenException">工厂/车间/产线超出授权。</exception>
    public static OrganizationScopeFilter Intersect(
        IDataScope userScope,
        long factoryId,
        long? workshopId,
        long? productionLineId)
    {
        ArgumentNullException.ThrowIfNull(userScope);

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId));
        }

        if (!userScope.AllowsFactory(factoryId))
        {
            throw DataScopeForbiddenException.ForFactory(factoryId);
        }

        if (workshopId is not null
            && !userScope.AllowsWorkshop(factoryId, workshopId.Value))
        {
            throw DataScopeForbiddenException.ForWorkshop(factoryId, workshopId.Value);
        }

        if (productionLineId is not null)
        {
            var workshopForLine = workshopId
                ?? InferWorkshopForProductionLine(userScope, factoryId, productionLineId.Value);

            if (workshopForLine is null
                || !userScope.AllowsProductionLine(factoryId, workshopForLine.Value, productionLineId.Value))
            {
                throw DataScopeForbiddenException.ForProductionLine(
                    factoryId,
                    workshopForLine,
                    productionLineId.Value);
            }

            workshopId ??= workshopForLine;
        }

        var authorizedWorkshops = userScope.GetAuthorizedWorkshopIds(factoryId);
        var authorizedLines = userScope.GetAuthorizedProductionLineIds(factoryId);

        long? effectiveWorkshop = workshopId;
        long? effectiveLine = productionLineId;
        IReadOnlySet<long>? restrictWorkshops = null;
        IReadOnlySet<long>? restrictLines = null;

        if (effectiveWorkshop is null)
        {
            if (authorizedWorkshops is { Count: 1 })
            {
                effectiveWorkshop = authorizedWorkshops.First();
            }
            else if (authorizedWorkshops is { Count: > 1 })
            {
                restrictWorkshops = authorizedWorkshops;
            }
        }

        if (effectiveLine is null)
        {
            if (authorizedLines is { Count: 1 })
            {
                effectiveLine = authorizedLines.First();
                effectiveWorkshop ??= InferWorkshopForProductionLine(userScope, factoryId, effectiveLine.Value);
            }
            else if (authorizedLines is { Count: > 1 })
            {
                restrictLines = authorizedLines;
            }
        }

        return new OrganizationScopeFilter(
            factoryId,
            effectiveWorkshop,
            effectiveLine,
            restrictWorkshops,
            restrictLines);
    }

    private static long? InferWorkshopForProductionLine(
        IDataScope userScope,
        long factoryId,
        long productionLineId)
    {
        if (userScope.IsGlobal)
        {
            return null;
        }

        return userScope.Grants
            .Where(g => g.FactoryId == factoryId && g.ProductionLineId == productionLineId)
            .Select(g => g.WorkshopId)
            .FirstOrDefault(id => id is not null);
    }
}
