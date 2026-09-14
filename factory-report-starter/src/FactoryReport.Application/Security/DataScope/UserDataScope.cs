namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 用户数据范围的不可变实现。
/// </summary>
public sealed class UserDataScope : IDataScope
{
    public static UserDataScope Global { get; } = new(isGlobal: true, grants: []);

    public static UserDataScope Empty { get; } = new(isGlobal: false, grants: []);

    public bool IsGlobal { get; }

    public IReadOnlyList<DataScopeGrant> Grants { get; }

    public UserDataScope(bool isGlobal, IReadOnlyList<DataScopeGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        IsGlobal = isGlobal;
        Grants = grants;
    }

    public static UserDataScope FromGrants(params DataScopeGrant[] grants)
        => new(isGlobal: false, grants);

    public bool AllowsFactory(long factoryId)
    {
        if (factoryId <= 0)
        {
            return false;
        }

        if (IsGlobal)
        {
            return true;
        }

        return Grants.Any(g => g.FactoryId == factoryId);
    }

    public bool AllowsWorkshop(long factoryId, long workshopId)
    {
        if (!AllowsFactory(factoryId) || workshopId <= 0)
        {
            return false;
        }

        if (IsGlobal)
        {
            return true;
        }

        var factoryGrants = Grants.Where(g => g.FactoryId == factoryId).ToList();
        if (factoryGrants.Count == 0)
        {
            return false;
        }

        // 工厂级授权：该工厂内全部车间。
        if (factoryGrants.Any(g => g.IsFactoryWide))
        {
            return true;
        }

        return factoryGrants.Any(g => g.WorkshopId == workshopId);
    }

    public bool AllowsProductionLine(long factoryId, long workshopId, long productionLineId)
    {
        if (!AllowsWorkshop(factoryId, workshopId) || productionLineId <= 0)
        {
            return false;
        }

        if (IsGlobal)
        {
            return true;
        }

        var factoryGrants = Grants.Where(g => g.FactoryId == factoryId).ToList();
        if (factoryGrants.Any(g => g.IsFactoryWide))
        {
            return true;
        }

        var workshopGrants = factoryGrants.Where(g => g.WorkshopId == workshopId).ToList();
        if (workshopGrants.Count == 0)
        {
            return false;
        }

        // 车间级授权：该车间内全部产线。
        if (workshopGrants.Any(g => g.IsWorkshopWide))
        {
            return true;
        }

        return workshopGrants.Any(g => g.ProductionLineId == productionLineId);
    }

    public IReadOnlySet<long>? GetAuthorizedWorkshopIds(long factoryId)
    {
        if (IsGlobal || !AllowsFactory(factoryId))
        {
            return null;
        }

        var factoryGrants = Grants.Where(g => g.FactoryId == factoryId).ToList();
        if (factoryGrants.Any(g => g.IsFactoryWide))
        {
            return null;
        }

        return factoryGrants
            .Where(g => g.WorkshopId is not null)
            .Select(g => g.WorkshopId!.Value)
            .ToHashSet();
    }

    public IReadOnlySet<long>? GetAuthorizedProductionLineIds(long factoryId)
    {
        if (IsGlobal || !AllowsFactory(factoryId))
        {
            return null;
        }

        var factoryGrants = Grants.Where(g => g.FactoryId == factoryId).ToList();
        if (factoryGrants.Any(g => g.IsFactoryWide))
        {
            return null;
        }

        // 任一车间级（无产线限制）授权 → 产线在已授权车间内不额外限制集合。
        if (factoryGrants.Any(g => g.IsWorkshopWide))
        {
            // 仍可能混有产线级授权；仅当全部为产线级时才返回限制集合。
            var lineOnly = factoryGrants.Where(g => g.ProductionLineId is not null).ToList();
            var workshopWide = factoryGrants.Where(g => g.IsWorkshopWide).ToList();
            if (workshopWide.Count > 0)
            {
                return null;
            }

            return lineOnly.Select(g => g.ProductionLineId!.Value).ToHashSet();
        }

        return factoryGrants
            .Where(g => g.ProductionLineId is not null)
            .Select(g => g.ProductionLineId!.Value)
            .ToHashSet();
    }
}
