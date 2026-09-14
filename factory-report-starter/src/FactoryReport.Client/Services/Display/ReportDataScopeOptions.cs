using FactoryReport.Application.Security.DataScope;

namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 根据 /me 的 dataScope 生成报表筛选下拉选项；不在范围内的组织不出现。
/// </summary>
public static class ReportDataScopeOptions
{
    /// <summary>
    /// 可访问工厂列表。全局范围返回 Fake 演示工厂目录且须用户显式选择（不自动选中）。
    /// </summary>
    public static IReadOnlyList<OrgOption> GetFactoryOptions(DataScopeSummary? scope)
    {
        if (scope is null)
        {
            return [];
        }

        if (scope.IsGlobal)
        {
            return FakeDemoOrgCatalog.Factories;
        }

        return scope.Grants
            .Select(g => g.FactoryId)
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => FakeDemoOrgCatalog.Factories.FirstOrDefault(f => f.Id == id) is { Id: > 0 } labeled
                ? labeled
                : new OrgOption(id, $"工厂 {id}"))
            .ToArray();
    }

    /// <summary>
    /// 当前工厂下可选车间。仅展示授权范围内的车间；全局/工厂级授权时展开 Fake 目录中该厂车间。
    /// </summary>
    public static IReadOnlyList<OrgOption> GetWorkshopOptions(DataScopeSummary? scope, long? factoryId)
    {
        if (scope is null || factoryId is null or <= 0 || !AllowsFactory(scope, factoryId.Value))
        {
            return [];
        }

        if (scope.IsGlobal || HasFactoryWideGrant(scope, factoryId.Value))
        {
            return FakeDemoOrgCatalog.Workshops
                .Where(w => w.FactoryId == factoryId.Value)
                .Select(w => w.Option)
                .ToArray();
        }

        return scope.Grants
            .Where(g => g.FactoryId == factoryId.Value && g.WorkshopId is > 0)
            .Select(g => g.WorkshopId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .Select(id =>
            {
                var labeled = FakeDemoOrgCatalog.Workshops
                    .FirstOrDefault(w => w.FactoryId == factoryId.Value && w.Option.Id == id);
                return labeled.Option.Id > 0
                    ? labeled.Option
                    : new OrgOption(id, $"车间 {id}");
            })
            .ToArray();
    }

    /// <summary>
    /// 当前工厂/车间下可选产线。仅展示授权范围内的产线。
    /// </summary>
    public static IReadOnlyList<OrgOption> GetProductionLineOptions(
        DataScopeSummary? scope,
        long? factoryId,
        long? workshopId)
    {
        if (scope is null || factoryId is null or <= 0 || !AllowsFactory(scope, factoryId.Value))
        {
            return [];
        }

        if (workshopId is null or <= 0)
        {
            return [];
        }

        if (!AllowsWorkshop(scope, factoryId.Value, workshopId.Value))
        {
            return [];
        }

        if (scope.IsGlobal
            || HasFactoryWideGrant(scope, factoryId.Value)
            || HasWorkshopWideGrant(scope, factoryId.Value, workshopId.Value))
        {
            return FakeDemoOrgCatalog.Lines
                .Where(l => l.FactoryId == factoryId.Value && l.WorkshopId == workshopId.Value)
                .Select(l => l.Option)
                .ToArray();
        }

        return scope.Grants
            .Where(g =>
                g.FactoryId == factoryId.Value
                && g.WorkshopId == workshopId.Value
                && g.ProductionLineId is > 0)
            .Select(g => g.ProductionLineId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .Select(id =>
            {
                var labeled = FakeDemoOrgCatalog.Lines
                    .FirstOrDefault(l =>
                        l.FactoryId == factoryId.Value
                        && l.WorkshopId == workshopId.Value
                        && l.Option.Id == id);
                return labeled.Option.Id > 0
                    ? labeled.Option
                    : new OrgOption(id, $"产线 {id}");
            })
            .ToArray();
    }

    public static bool AllowsFactory(DataScopeSummary scope, long factoryId)
    {
        if (factoryId <= 0)
        {
            return false;
        }

        return scope.IsGlobal || scope.Grants.Any(g => g.FactoryId == factoryId);
    }

    public static bool AllowsWorkshop(DataScopeSummary scope, long factoryId, long workshopId)
    {
        if (!AllowsFactory(scope, factoryId) || workshopId <= 0)
        {
            return false;
        }

        if (scope.IsGlobal || HasFactoryWideGrant(scope, factoryId))
        {
            return FakeDemoOrgCatalog.Workshops.Any(w =>
                w.FactoryId == factoryId && w.Option.Id == workshopId);
        }

        return scope.Grants.Any(g => g.FactoryId == factoryId && g.WorkshopId == workshopId);
    }

    public static bool AllowsProductionLine(
        DataScopeSummary scope,
        long factoryId,
        long workshopId,
        long productionLineId)
    {
        if (!AllowsWorkshop(scope, factoryId, workshopId) || productionLineId <= 0)
        {
            return false;
        }

        if (scope.IsGlobal
            || HasFactoryWideGrant(scope, factoryId)
            || HasWorkshopWideGrant(scope, factoryId, workshopId))
        {
            return FakeDemoOrgCatalog.Lines.Any(l =>
                l.FactoryId == factoryId
                && l.WorkshopId == workshopId
                && l.Option.Id == productionLineId);
        }

        return scope.Grants.Any(g =>
            g.FactoryId == factoryId
            && g.WorkshopId == workshopId
            && g.ProductionLineId == productionLineId);
    }

    /// <summary>
    /// 仅当恰好一个工厂可选时返回该 Id；全局或多厂返回 null（须显式选择）。
    /// </summary>
    public static long? TryAutoSelectFactory(DataScopeSummary? scope)
    {
        if (scope is null || scope.IsGlobal)
        {
            return null;
        }

        var factories = GetFactoryOptions(scope);
        return factories.Count == 1 ? factories[0].Id : null;
    }

    private static bool HasFactoryWideGrant(DataScopeSummary scope, long factoryId)
        => scope.Grants.Any(g => g.FactoryId == factoryId && g.WorkshopId is null);

    private static bool HasWorkshopWideGrant(DataScopeSummary scope, long factoryId, long workshopId)
        => scope.Grants.Any(g =>
            g.FactoryId == factoryId
            && g.WorkshopId == workshopId
            && g.ProductionLineId is null);
}
