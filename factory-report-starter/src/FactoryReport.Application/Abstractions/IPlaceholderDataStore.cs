namespace FactoryReport.Application.Abstractions;

/// <summary>
/// 占位数据访问抽象。阶段 1 仅提供 Fake 内存实现；
/// 现场 Oracle 持久化将在 Infrastructure 中实现本接口（或拆分后的仓储接口）。
/// </summary>
public interface IPlaceholderDataStore
{
    string Describe();
}
