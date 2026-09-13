using FactoryReport.Application.Abstractions;

namespace FactoryReport.Infrastructure.Fake;

/// <summary>
/// 默认 Fake 模式：永不连接数据库。
/// </summary>
public sealed class FakeDataAccessModeProvider : IDataAccessModeProvider
{
    public DataAccessMode Mode => DataAccessMode.Fake;

    public bool IsFake => true;
}
