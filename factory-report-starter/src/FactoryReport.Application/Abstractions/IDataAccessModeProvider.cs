namespace FactoryReport.Application.Abstractions;

/// <summary>
/// 当前运行数据模式。后续 Oracle 实现替换点留在 Infrastructure。
/// </summary>
public interface IDataAccessModeProvider
{
    DataAccessMode Mode { get; }

    bool IsFake { get; }
}
