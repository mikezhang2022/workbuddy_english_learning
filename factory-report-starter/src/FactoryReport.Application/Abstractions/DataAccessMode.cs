namespace FactoryReport.Application.Abstractions;

/// <summary>
/// 数据访问模式。默认 Fake：内存数据，不连接任何数据库。
/// </summary>
public enum DataAccessMode
{
    /// <summary>内存 Fake 数据，禁止连接 Oracle / MES。</summary>
    Fake = 0,

    /// <summary>现场 Oracle 持久化。【待现场确认】不得在云端启用。</summary>
    Oracle = 1
}
