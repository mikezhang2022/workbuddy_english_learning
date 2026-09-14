using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryReport.Infrastructure.Options;

/// <summary>
/// 启动时校验 FactoryReport 配置，避免无效 DataMode 或危险默认值 silently 通过。
/// </summary>
public sealed class FactoryReportOptionsValidator : IValidateOptions<FactoryReportOptions>
{
    public ValidateOptionsResult Validate(string? name, FactoryReportOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("FactoryReport options are required.");
        }

        if (string.IsNullOrWhiteSpace(options.DataMode))
        {
            return ValidateOptionsResult.Fail("FactoryReport:DataMode is required. Use Fake (default) or Oracle.");
        }

        if (!Enum.TryParse<DataAccessMode>(options.DataMode.Trim(), ignoreCase: true, out _))
        {
            return ValidateOptionsResult.Fail(
                $"FactoryReport:DataMode '{options.DataMode}' is invalid. Allowed values: Fake, Oracle.");
        }

        var heartbeat = options.Worker?.HeartbeatIntervalSeconds ?? 0;
        if (heartbeat < 5 || heartbeat > 3600)
        {
            return ValidateOptionsResult.Fail(
                "FactoryReport:Worker:HeartbeatIntervalSeconds must be between 5 and 3600.");
        }

        var accountStore = options.Authentication?.AccountStore?.Trim();
        if (string.IsNullOrWhiteSpace(accountStore))
        {
            return ValidateOptionsResult.Fail(
                "FactoryReport:Authentication:AccountStore is required. Use Fake (dev/test) or Oracle (future).");
        }

        if (!string.Equals(accountStore, "Fake", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(accountStore, "Oracle", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"FactoryReport:Authentication:AccountStore '{accountStore}' is invalid. Allowed values: Fake, Oracle.");
        }

        return ValidateOptionsResult.Success;
    }
}
