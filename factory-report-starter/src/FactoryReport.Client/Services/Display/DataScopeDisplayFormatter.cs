using FactoryReport.Application.Security.DataScope;

namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 将服务端返回的 dataScope 摘要格式化为可读中文（仅展示已授权范围）。
/// </summary>
public static class DataScopeDisplayFormatter
{
    public static IReadOnlyList<string> FormatSummaryLines(DataScopeSummary? scope)
    {
        if (scope is null)
        {
            return ["数据范围：未配置"];
        }

        if (scope.IsGlobal)
        {
            return ["数据范围：全局"];
        }

        if (scope.Grants.Count == 0)
        {
            return ["数据范围：未配置"];
        }

        return scope.Grants
            .Select(FormatGrant)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatGrant(DataScopeGrantSummary grant)
    {
        if (grant.ProductionLineId is not null)
        {
            return $"工厂 {grant.FactoryId} / 车间 {grant.WorkshopId} / 产线 {grant.ProductionLineId}";
        }

        if (grant.WorkshopId is not null)
        {
            return $"工厂 {grant.FactoryId} / 车间 {grant.WorkshopId}";
        }

        return $"工厂 {grant.FactoryId}（全厂）";
    }
}
