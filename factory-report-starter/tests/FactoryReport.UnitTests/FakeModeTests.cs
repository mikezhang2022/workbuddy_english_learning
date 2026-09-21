using FactoryReport.Infrastructure.Fake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryReport.UnitTests;

public class FakeModeTests
{
    [Fact]
    public void FakeDataAccessModeProvider_IsFake_AndNeverOracle()
    {
        var provider = new FakeDataAccessModeProvider();

        Assert.True(provider.IsFake);
        Assert.Equal(Application.Abstractions.DataAccessMode.Fake, provider.Mode);
    }

    [Fact]
    public void FakePlaceholderDataStore_DescribesInMemoryStore()
    {
        var store = new FakePlaceholderDataStore();

        Assert.Contains("Fake", store.Describe(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no database", store.Describe(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddInfrastructure_Throws_WhenDataModeIsOracle_DoesNotSilentlyFallBackToFake()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FactoryReport:DataMode"] = "Oracle",
                ["FactoryReport:Authentication:AccountStore"] = "Fake",
                ["FactoryReport:Worker:HeartbeatIntervalSeconds"] = "30"
            })
            .Build();

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => FactoryReport.Infrastructure.DependencyInjection.AddInfrastructure(services, configuration));

        Assert.Contains("DataMode=Oracle", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not implemented", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(FactoryReport.Application.Abstractions.IDataAccessModeProvider));
    }

    [Fact]
    public void AddInfrastructure_Throws_WhenAccountStoreIsOracle_DoesNotSilentlyFallBackToFake()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FactoryReport:DataMode"] = "Fake",
                ["FactoryReport:Authentication:AccountStore"] = "Oracle",
                ["FactoryReport:Worker:HeartbeatIntervalSeconds"] = "30"
            })
            .Build();

        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => FactoryReport.Infrastructure.DependencyInjection.AddInfrastructure(services, configuration));

        Assert.Contains("AccountStore=Oracle", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(FactoryReport.Application.Security.ILocalAccountStore));
    }
}
