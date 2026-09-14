using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Configuration;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Reporting.ProductionDaily;
using FactoryReport.Infrastructure.Fake;
using FactoryReport.Infrastructure.Options;
using FactoryReport.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FactoryReport.Infrastructure;

public static class DependencyInjection
{
    public const string DataModeConfigKey = "FactoryReport:DataMode";

    /// <summary>
    /// 注册基础设施与强类型运行配置。默认 Fake，不连接任何数据库。
    /// DataMode=Oracle 仅作占位识别，本阶段仍强制走 Fake，避免误连。
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<FactoryReportOptions>, FactoryReportOptionsValidator>();
        services.AddOptions<FactoryReportOptions>()
            .Bind(configuration.GetSection(FactoryReportOptions.SectionName))
            .ValidateOnStart();

        var configuredMode = configuration.GetValue<string>(DataModeConfigKey) ?? nameof(DataAccessMode.Fake);

        // 阶段 4/5：无论配置为何，一律注册 Fake 实现，禁止真实 Oracle / MES 连接。
        services.AddSingleton<IDataAccessModeProvider, FakeDataAccessModeProvider>();
        services.AddSingleton<IPlaceholderDataStore, FakePlaceholderDataStore>();
        services.AddSingleton<IUtcClock, SystemUtcClock>();

        // 确定性夹具快照（进程内单例）；『仅用于开发测试，不代表现场 MES 正式口径』。
        services.AddSingleton(_ => DeterministicFakeFixture.Create());

        services.AddSingleton<IOrganizationReadRepository, FakeOrganizationReadRepository>();
        services.AddSingleton<IProductReadRepository, FakeProductReadRepository>();
        services.AddSingleton<IWorkOrderReadRepository, FakeWorkOrderReadRepository>();
        services.AddSingleton<IProductionRecordReadRepository, FakeProductionRecordReadRepository>();
        services.AddSingleton<IDailyProductionPlanReadRepository, FakeDailyProductionPlanReadRepository>();
        services.AddSingleton<IImportBatchReadRepository, FakeImportBatchReadRepository>();
        services.AddSingleton<IReportDataQueryService, ReportDataQueryService>();
        services.AddSingleton<IProductionDailyReportService, ProductionDailyReportService>();

        // 【待现场确认】未来 Oracle 替换点：
        // 1. 在 Persistence/Oracle/ 实现上述 I*ReadRepository 接口（DbContext / ODP.NET）。
        // 2. 按 DataMode=Oracle 在本方法切换注册（凭据由服务器环境注入，不得写入仓库）。
        // 3. 不得在 Cursor Cloud 启用 Oracle 模式。
        // 见 OraclePersistencePlaceholder 与 docs/fake-data.md。
        _ = configuredMode;

        return services;
    }
}
