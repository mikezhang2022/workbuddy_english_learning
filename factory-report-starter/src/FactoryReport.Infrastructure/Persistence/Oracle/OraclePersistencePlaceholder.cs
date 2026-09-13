namespace FactoryReport.Infrastructure.Persistence.Oracle;

/// <summary>
/// 【待现场确认】Oracle 持久化接入点占位。
/// 阶段 1 不配置连接字符串、不创建 DbContext、不执行迁移、不连接 Oracle。
/// 后续现场接入时在本目录实现：
/// - DbContext / 仓储
/// - Oracle.EntityFrameworkCore 注册
/// - ODP.NET 连接（凭据由服务器环境注入，不得写入仓库）
/// </summary>
public static class OraclePersistencePlaceholder
{
    public const string Status = "NotConfigured_PendingOnSiteConfirmation";
}
