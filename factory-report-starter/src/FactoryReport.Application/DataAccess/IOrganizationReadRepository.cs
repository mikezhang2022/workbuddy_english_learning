using FactoryReport.Domain.Organizations;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 组织范围只读访问。未来 Oracle 实现【待现场确认】替换点见 Infrastructure DI。
/// </summary>
public interface IOrganizationReadRepository
{
    Task<IReadOnlyList<Factory>> GetFactoriesAsync(CancellationToken cancellationToken = default);

    Task<Factory?> GetFactoryByIdAsync(long factoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Workshop>> GetWorkshopsAsync(long factoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductionLine>> GetProductionLinesAsync(
        long factoryId,
        long? workshopId = null,
        CancellationToken cancellationToken = default);
}
