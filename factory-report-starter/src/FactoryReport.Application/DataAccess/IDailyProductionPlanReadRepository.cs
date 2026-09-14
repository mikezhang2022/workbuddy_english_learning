using FactoryReport.Domain.Planning;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 日计划行只读访问。必须带 FactoryId 与日期范围。未来 Oracle 实现【待现场确认】。
/// </summary>
public interface IDailyProductionPlanReadRepository
{
    Task<IReadOnlyList<DailyProductionPlanLine>> GetDailyPlanLinesAsync(
        OrganizationScopeFilter scope,
        DateRangeFilter dateRange,
        string? productCode = null,
        CancellationToken cancellationToken = default);
}
