namespace FactoryReport.Domain.MasterData;

/// <summary>
/// 产品主数据标识。保留 FactoryId 以支持多工厂。
/// </summary>
public sealed class Product
{
    public long Id { get; }
    public long FactoryId { get; }
    public string ProductCode { get; }
    public string Name { get; }

    public Product(long id, long factoryId, string productCode, string name)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Product Id must be a positive identifier.");
        }

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        FactoryId = factoryId;
        ProductCode = productCode.Trim();
        Name = name.Trim();
    }
}
