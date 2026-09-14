namespace FactoryReport.Domain.Organizations;

/// <summary>
/// 产线组织，归属工厂与车间。
/// </summary>
public sealed class ProductionLine
{
    public long Id { get; }
    public long FactoryId { get; }
    public long WorkshopId { get; }
    public string Code { get; }
    public string Name { get; }

    public ProductionLine(long id, long factoryId, long workshopId, string code, string name)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "ProductionLineId must be a positive identifier.");
        }

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        if (workshopId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workshopId), "WorkshopId must be a positive identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        FactoryId = factoryId;
        WorkshopId = workshopId;
        Code = code.Trim();
        Name = name.Trim();
    }
}
