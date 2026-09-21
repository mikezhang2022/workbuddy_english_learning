using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Configuration;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Import;
using FactoryReport.Application.Reporting.MonthlyProductionPlan;
using FactoryReport.Application.Reporting.ProductionDaily;
using FactoryReport.Application.Reporting.ProductionPlanAchievement;
using FactoryReport.Application.Reporting.QualityStatistics;
using FactoryReport.Application.Reporting.WorkOrderProgress;
using FactoryReport.Application.Security;
using FactoryReport.Application.Security.DataScope;
using FactoryReport.Infrastructure.Excel;
using FactoryReport.Infrastructure.Fake;
using FactoryReport.Infrastructure.Import;
using FactoryReport.Infrastructure.Options;
using FactoryReport.Infrastructure.Security;
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
    /// DataMode=Oracle / AccountStore=Oracle 本阶段均未实现：启动时显式失败，禁止静默回退 Fake。
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

        if (string.Equals(configuredMode, nameof(DataAccessMode.Oracle), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "FactoryReport:DataMode=Oracle is reserved but not implemented in this phase. " +
                "Use Fake for Development/Testing only. Real Oracle access requires on-site implementation " +
                "under Persistence/Oracle/ and must not be simulated by silently falling back to Fake. " +
                "See docs/architecture.md and docs/oracle-integration-plan.md.");
        }

        if (!string.Equals(configuredMode, nameof(DataAccessMode.Fake), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"FactoryReport:DataMode '{configuredMode}' is not supported. Allowed: Fake, Oracle.");
        }

        // Fake 模式：注册确定性内存仓储，禁止真实 Oracle / MES 连接。
        services.AddSingleton<IDataAccessModeProvider, FakeDataAccessModeProvider>();
        services.AddSingleton<IPlaceholderDataStore, FakePlaceholderDataStore>();
        services.AddSingleton<IUtcClock, SystemUtcClock>();

        // 确定性夹具快照（进程内单例）；『仅用于开发测试，不代表现场 MES 正式口径』。
        services.AddSingleton(_ => DeterministicFakeFixture.Create());

        services.AddSingleton<IImportBatchWorkspace, FakeImportBatchWorkspace>();
        services.AddSingleton<IExcelWorkbookService, MiniExcelWorkbookService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();

        services.AddSingleton<IOrganizationReadRepository, FakeOrganizationReadRepository>();
        services.AddSingleton<IProductReadRepository, FakeProductReadRepository>();
        services.AddSingleton<IWorkOrderReadRepository, FakeWorkOrderReadRepository>();
        services.AddSingleton<IProductionRecordReadRepository, FakeProductionRecordReadRepository>();
        services.AddSingleton<IDailyProductionPlanReadRepository, FakeDailyProductionPlanReadRepository>();
        services.AddSingleton<IImportBatchReadRepository, FakeImportBatchReadRepository>();
        // Scoped：报表服务依赖 Scoped 的 IReportQueryScopeService / ICurrentUserAccessor，
        // 不可注册为 Singleton，否则 Development ValidateScopes 会失败。
        services.AddScoped<IReportDataQueryService, ReportDataQueryService>();
        services.AddScoped<IProductionDailyReportService, ProductionDailyReportService>();
        services.AddScoped<IWorkOrderProgressReportService, WorkOrderProgressReportService>();
        services.AddScoped<IQualityStatisticsReportService, QualityStatisticsReportService>();
        services.AddScoped<IProductionPlanAchievementReportService, ProductionPlanAchievementReportService>();
        services.AddScoped<IMonthlyProductionPlanReportService, MonthlyProductionPlanReportService>();

        // 本地账号认证。
        // Fake：进程内测试账号（Development/Testing）。
        // Oracle：仅预留接口；本阶段未实现，配置为 Oracle 时显式失败，禁止静默回退 Fake。
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ILocalAccountAuthenticationService, LocalAccountAuthenticationService>();
        services.AddSingleton<IUserScopeResolver, LocalAccountUserScopeResolver>();
        services.AddScoped<IReportQueryScopeService, ReportQueryScopeService>();

        var accountStore = configuration
            .GetSection(FactoryReportOptions.SectionName)
            .GetSection("Authentication")
            .GetValue<string>("AccountStore") ?? FakeLocalAccountStore.StoreKindName;

        if (string.Equals(accountStore, FakeLocalAccountStore.StoreKindName, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ILocalAccountStore, FakeLocalAccountStore>();
        }
        else if (string.Equals(accountStore, "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "FactoryReport:Authentication:AccountStore=Oracle is reserved but not implemented in this phase. " +
                "Use Fake for Development/Testing only. See docs/authentication.md and Persistence/Oracle placeholder.");
        }
        else
        {
            throw new InvalidOperationException(
                $"FactoryReport:Authentication:AccountStore '{accountStore}' is not supported. Allowed: Fake, Oracle.");
        }

        // 【待现场确认】未来 Oracle 替换点：
        // 1. 在 Persistence/Oracle/ 实现上述 I*ReadRepository 接口（DbContext / ODP.NET）。
        // 2. 实现 ILocalAccountStore 的 Oracle 版本（正式账号/密码哈希/角色/组织授权），按 AccountStore=Oracle 切换（替换上方 throw）。
        // 3. 实现 IUserScopeResolver 的 Oracle 版本（或复用 LocalAccountUserScopeResolver + Oracle store）。
        // 4. 按 DataMode=Oracle 在本方法切换仓储注册（凭据由服务器环境注入，不得写入仓库）；实现前保持显式失败。
        // 5. 不得在 Cursor Cloud 启用 Oracle 模式。
        // 见 OraclePersistencePlaceholder、docs/fake-data.md、docs/authentication.md、docs/authorization-and-data-scope.md。

        return services;
    }
}
