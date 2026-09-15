using FactoryReport.Application.Security;
using FactoryReport.Infrastructure;
using FactoryReport.Worker.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FactoryReport.Worker;

/// <summary>
/// Worker DI 注册，供 Program 与回归测试共用。
/// </summary>
public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddWorkerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddInfrastructure(configuration);
        services.AddScoped<ICurrentUserAccessor, AnonymousCurrentUserAccessor>();
        services.AddHostedService<Worker>();
        return services;
    }
}
