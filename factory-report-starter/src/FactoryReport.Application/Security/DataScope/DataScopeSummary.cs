namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 可读的数据范围摘要（供 /me；不含敏感权限细节以外的运维机密）。
/// </summary>
public sealed class DataScopeSummary
{
    public bool IsGlobal { get; init; }
    public required IReadOnlyList<DataScopeGrantSummary> Grants { get; init; }

    public static DataScopeSummary From(IDataScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return new DataScopeSummary
        {
            IsGlobal = scope.IsGlobal,
            Grants = scope.Grants
                .Select(g => new DataScopeGrantSummary
                {
                    FactoryId = g.FactoryId,
                    WorkshopId = g.WorkshopId,
                    ProductionLineId = g.ProductionLineId
                })
                .ToArray()
        };
    }
}

public sealed class DataScopeGrantSummary
{
    public long FactoryId { get; init; }
    public long? WorkshopId { get; init; }
    public long? ProductionLineId { get; init; }
}
