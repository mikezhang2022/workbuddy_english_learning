using FactoryReport.Application.Configuration;
using FactoryReport.Infrastructure.Fake;
using Microsoft.Extensions.Hosting;

namespace FactoryReport.Infrastructure.Security;

/// <summary>
/// Production 环境禁止启用 Fake 账号存储：启动时显式失败，禁止静默带着测试账号上线。
/// </summary>
public static class FakeAccountStoreProductionGuard
{
    public const string RejectionMessage =
        "FactoryReport Fake local account store must not run in Production. " +
        "Set FactoryReport:Authentication:AccountStore to Oracle (future) or do not deploy with Fake accounts. " +
        "Test-only accounts are for Development/Testing only.";

    public static void EnsureNotFakeInProduction(IHostEnvironment environment, FactoryReportOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        if (!environment.IsProduction())
        {
            return;
        }

        var store = options.Authentication?.AccountStore?.Trim() ?? FakeLocalAccountStore.StoreKindName;
        if (string.Equals(store, FakeLocalAccountStore.StoreKindName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(store, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(RejectionMessage);
        }
    }
}
