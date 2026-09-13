using FactoryReport.Application.Abstractions;
using FactoryReport.Infrastructure.Fake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryReport.Infrastructure;

public static class DependencyInjection
{
    public const string DataModeConfigKey = "FactoryReport:DataMode";

    /// <summary>
    /// 注册基础设施。默认 Fake，不连接任何数据库。
    /// DataMode=Oracle 仅作占位识别，阶段 1 仍强制走 Fake，避免误连。
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredMode = configuration.GetValue<string>(DataModeConfigKey) ?? nameof(DataAccessMode.Fake);

        // 阶段 1：无论配置为何，一律注册 Fake 实现，禁止真实 Oracle 连接。
        services.AddSingleton<IDataAccessModeProvider, FakeDataAccessModeProvider>();
        services.AddSingleton<IPlaceholderDataStore, FakePlaceholderDataStore>();

        _ = configuredMode; // 保留配置读取，供后续阶段切换使用

        return services;
    }
}
