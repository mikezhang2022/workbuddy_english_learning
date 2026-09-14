using FactoryReport.Domain.MasterData;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 工单只读访问。未来 Oracle 实现【待现场确认】。
/// </summary>
public interface IWorkOrderReadRepository
{
    Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(
        OrganizationScopeFilter scope,
        string? productCode = null,
        CancellationToken cancellationToken = default);
}
