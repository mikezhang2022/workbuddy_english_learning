namespace FactoryReport.Application.Configuration;

/// <summary>
/// 运行时强类型配置（Options pattern）。默认 Fake，禁止在云端连接 Oracle/MES。
/// </summary>
public sealed class FactoryReportOptions
{
    public const string SectionName = "FactoryReport";

    /// <summary>
    /// 数据访问模式：Fake 或 Oracle。默认 Fake。
    /// </summary>
    public string DataMode { get; set; } = "Fake";

    /// <summary>
    /// 仅测试宿主使用：暴露故意抛错的端点以验证 ProblemDetails。生产必须为 false。
    /// </summary>
    public bool ExposeTestExceptionEndpoint { get; set; }

    /// <summary>
    /// Worker 运行参数。
    /// </summary>
    public WorkerRuntimeOptions Worker { get; set; } = new();
}

/// <summary>
/// Worker 心跳与运行参数。
/// </summary>
public sealed class WorkerRuntimeOptions
{
    /// <summary>
    /// 心跳间隔（秒）。默认 30。
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;
}
