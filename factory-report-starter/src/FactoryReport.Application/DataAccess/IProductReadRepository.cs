using FactoryReport.Domain.MasterData;

namespace FactoryReport.Application.DataAccess;

/// <summary>
/// 产品主数据只读访问。未来 Oracle 实现【待现场确认】。
/// </summary>
public interface IProductReadRepository
{
    Task<IReadOnlyList<Product>> GetProductsAsync(
        long factoryId,
        string? productCode = null,
        CancellationToken cancellationToken = default);
}
