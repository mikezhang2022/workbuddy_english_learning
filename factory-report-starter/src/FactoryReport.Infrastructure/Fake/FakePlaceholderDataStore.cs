using FactoryReport.Application.Abstractions;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// 内存占位数据存储。后续由 Oracle 持久化实现替换（见 Persistence/Oracle）。
/// </summary>
public sealed class FakePlaceholderDataStore : IPlaceholderDataStore
{
    public string Describe() => "In-memory Fake store (no database connection)";
}
