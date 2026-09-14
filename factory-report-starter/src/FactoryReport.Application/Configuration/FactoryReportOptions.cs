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

    /// <summary>
    /// 本地账号认证配置（阶段 10）。默认 Fake；Production 不得启用 Fake。
    /// </summary>
    public AuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// 移动 PWA 客户端源（CORS，含凭据）。示例仅 localhost；现场由反向代理或配置注入。
    /// </summary>
    public string[] MobileClientAllowedOrigins { get; set; } = [];
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

/// <summary>
/// 认证相关运行配置。
/// </summary>
public sealed class AuthenticationOptions
{
    /// <summary>
    /// 账号存储实现：Fake（开发/测试内存）或 Oracle（未来现场持久化，本阶段未实现）。
    /// 默认 Fake。Production + Fake 将在启动时失败。
    /// </summary>
    public string AccountStore { get; set; } = "Fake";
}
