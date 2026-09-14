using FactoryReport.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FactoryReport.Api.Health;

/// <summary>
/// Fake 模式下的就绪检查：始终成功，且不连接 Oracle、MES 或任何外部服务。
/// </summary>
public sealed class FakeReadinessHealthCheck(IDataAccessModeProvider modeProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // 阶段 2：Infrastructure 强制 Fake。就绪仅表示进程与 DI 可用，禁止探测外部系统。
        if (modeProvider.IsFake)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "Fake mode readiness OK (no Oracle/MES connectivity attempted)."));
        }

        // 占位：未来 Oracle 就绪检查【待现场确认】。当前仍不得连接外部服务。
        return Task.FromResult(HealthCheckResult.Healthy(
            "Readiness OK without external checks (Oracle readiness not enabled in this phase)."));
    }
}
