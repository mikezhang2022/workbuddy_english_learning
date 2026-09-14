namespace FactoryReport.Domain.Organizations;

/// <summary>
/// 工厂组织。业务表为多工厂预留 FactoryId，本实体为其根标识。
/// </summary>
public sealed class Factory
{
    public long Id { get; }
    public string Code { get; }
    public string Name { get; }

    public Factory(long id, string code, string name)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "FactoryId must be a positive identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
    }
}
