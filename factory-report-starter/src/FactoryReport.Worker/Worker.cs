using FactoryReport.Application.Abstractions;

namespace FactoryReport.Worker;

/// <summary>
/// 空壳 BackgroundService。阶段 1 不读取/连接 Oracle 或 MES。
/// </summary>
public sealed class Worker(
    ILogger<Worker> logger,
    IDataAccessModeProvider modeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "FactoryReport.Worker started in {Mode} mode (skeleton only; no MES/Oracle sync).",
            modeProvider.Mode);

        while (!stoppingToken.IsCancellationRequested)
        {
            // 阶段 1：仅保活心跳日志，不做同步。
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
