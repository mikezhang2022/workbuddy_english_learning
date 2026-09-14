namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 已认证但组织数据范围不足。API 映射为 403 ProblemDetails。
/// </summary>
public sealed class DataScopeForbiddenException : Exception
{
    public const string DefaultTitle = "Forbidden";
    public const string DefaultDetail = "Access to the requested organization scope is denied.";

    public string Title { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public DataScopeForbiddenException(
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? title = null)
        : base(detail)
    {
        Title = title ?? DefaultTitle;
        Errors = errors ?? new Dictionary<string, string[]>
        {
            ["DataScope"] = [detail]
        };
    }

    public static DataScopeForbiddenException ForFactory(long factoryId)
        => new(
            DefaultDetail,
            new Dictionary<string, string[]>
            {
                ["FactoryId"] = [$"FactoryId {factoryId} is outside the authorized data scope."]
            });

    public static DataScopeForbiddenException ForWorkshop(long factoryId, long workshopId)
        => new(
            DefaultDetail,
            new Dictionary<string, string[]>
            {
                ["WorkshopId"] =
                [
                    $"WorkshopId {workshopId} (FactoryId {factoryId}) is outside the authorized data scope."
                ]
            });

    public static DataScopeForbiddenException ForProductionLine(
        long factoryId,
        long? workshopId,
        long productionLineId)
        => new(
            DefaultDetail,
            new Dictionary<string, string[]>
            {
                ["ProductionLineId"] =
                [
                    workshopId is null
                        ? $"ProductionLineId {productionLineId} (FactoryId {factoryId}) is outside the authorized data scope."
                        : $"ProductionLineId {productionLineId} (FactoryId {factoryId}, WorkshopId {workshopId}) is outside the authorized data scope."
                ]
            });

    public static DataScopeForbiddenException NotAuthenticated()
        => new(
            "Not authenticated.",
            new Dictionary<string, string[]>
            {
                ["User"] = ["Authentication is required."]
            },
            title: "Unauthorized");
}
