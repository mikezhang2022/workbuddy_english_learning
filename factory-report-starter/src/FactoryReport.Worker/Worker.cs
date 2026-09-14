using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryReport.Worker;

/// <summary>
/// 后台 Worker。阶段 2：仅启动/停止/心跳日志；Fake 模式，不读取/连接 Oracle 或 MES。
/// </summary>
public sealed class Worker(
    ILogger<Worker> logger,
    IDataAccessModeProvider modeProvider,
    IOptions<FactoryReportOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var heartbeatSeconds = options.Value.Worker.HeartbeatIntervalSeconds;

        logger.LogInformation(
            "FactoryReport.Worker starting. Mode={Mode}, IsFake={IsFake}, HeartbeatSeconds={HeartbeatSeconds}. Runtime marked Fake: no MES/Oracle sync will run.",
            modeProvider.Mode,
            modeProvider.IsFake,
            heartbeatSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    "Worker heartbeat. Mode={Mode}, IsFake={IsFake}, Utc={Utc:o}",
                    modeProvider.Mode,
                    modeProvider.IsFake,
                    DateTimeOffset.UtcNow);

                await Task.Delay(TimeSpan.FromSeconds(heartbeatSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        finally
        {
            logger.LogInformation(
                "FactoryReport.Worker stopping. Mode={Mode}, IsFake={IsFake}.",
                modeProvider.Mode,
                modeProvider.IsFake);
        }
    }
}
