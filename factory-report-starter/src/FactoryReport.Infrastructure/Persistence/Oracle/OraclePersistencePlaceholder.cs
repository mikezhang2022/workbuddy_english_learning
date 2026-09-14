namespace FactoryReport.Infrastructure.Persistence.Oracle;

/// <summary>
/// 【待现场确认】Oracle 持久化接入点占位。
/// 本阶段不配置连接字符串、不创建 DbContext、不执行迁移、不连接 Oracle。
///
/// 未来现场接入时在本目录实现，并替换 Infrastructure/DependencyInjection.cs 中的 Fake 注册：
/// - 实现 Application.DataAccess 下的只读仓储接口：
///   IOrganizationReadRepository / IProductReadRepository / IWorkOrderReadRepository /
///   IProductionRecordReadRepository / IDailyProductionPlanReadRepository / IImportBatchReadRepository
/// - EF Core DbContext（Oracle.EntityFrameworkCore）
/// - ODP.NET / Oracle.ManagedDataAccess.Core 连接（凭据由服务器环境注入，不得写入仓库）
/// - Schema、字符集、连接方式、只读视图名称 —— 全部【待现场确认】
///
/// 当前默认 DataAccessMode=Fake；Cursor Cloud 禁止启用 Oracle。
/// </summary>
public static class OraclePersistencePlaceholder
{
    public const string Status = "NotConfigured_PendingOnSiteConfirmation";

    /// <summary>
    /// 未来 DI 切换时应注册的 Application 仓储接口清单（文档/导航用，无运行时行为）。
    /// </summary>
    public static readonly string[] ReplacementRepositoryInterfaces =
    [
        "FactoryReport.Application.DataAccess.IOrganizationReadRepository",
        "FactoryReport.Application.DataAccess.IProductReadRepository",
        "FactoryReport.Application.DataAccess.IWorkOrderReadRepository",
        "FactoryReport.Application.DataAccess.IProductionRecordReadRepository",
        "FactoryReport.Application.DataAccess.IDailyProductionPlanReadRepository",
        "FactoryReport.Application.DataAccess.IImportBatchReadRepository"
    ];
}
