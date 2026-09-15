using FactoryReport.Application.Reporting.ProductionDaily;
using FactoryReport.Application.Security;
using FactoryReport.Infrastructure;
using FactoryReport.Worker;
using FactoryReport.Worker.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FactoryReport.UnitTests.Worker;

public class WorkerCurrentUserAccessorTests
{
    [Fact]
    public async Task AddWorkerServices_ResolvesAnonymousCurrentUserAccessor_AndHostStarts()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FactoryReport:DataMode"] = "Fake",
                ["FactoryReport:Authentication:AccountStore"] = "Fake",
                ["FactoryReport:Worker:HeartbeatIntervalSeconds"] = "30"
            })
            .Build();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development,
            ApplicationName = "FactoryReport.Worker.Tests"
        });
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddWorkerServices(builder.Configuration);

        using var host = builder.Build();

        using (var scope = host.Services.CreateScope())
        {
            var accessor = scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();
            Assert.IsType<AnonymousCurrentUserAccessor>(accessor);
            Assert.Null(accessor.GetCurrentUser());

            // Development ValidateScopes：Scoped 报表服务可解析
            var report = scope.ServiceProvider.GetRequiredService<IProductionDailyReportService>();
            Assert.NotNull(report);
        }

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void Infrastructure_ReportServices_AreScoped_CompatibleWithCurrentUserAccessor()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FactoryReport:DataMode"] = "Fake",
                ["FactoryReport:Authentication:AccountStore"] = "Fake",
                ["FactoryReport:Worker:HeartbeatIntervalSeconds"] = "30"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUserAccessor, AnonymousCurrentUserAccessor>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductionDailyReportService>());
        Assert.Null(scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>().GetCurrentUser());
    }
}
