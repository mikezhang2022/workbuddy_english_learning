namespace FactoryReport.Domain.Organizations;

/// <summary>
/// 车间组织，归属某一工厂。
/// </summary>
public sealed class Workshop
{
    public long Id { get; }
    public long FactoryId { get; }
    public string Code { get; }
    public string Name { get; }

    public Workshop(long id, long factoryId, string code, string name)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "WorkshopId must be a positive identifier.");
        }

        if (factoryId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factoryId), "FactoryId must be a positive identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        FactoryId = factoryId;
        Code = code.Trim();
        Name = name.Trim();
    }
}
