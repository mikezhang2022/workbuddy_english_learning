using FactoryReport.Infrastructure.Fake;

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
}
