using FactoryReport.Application.Configuration;
using FactoryReport.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace FactoryReport.UnitTests;

public class FactoryReportOptionsValidatorTests
{
    private readonly FactoryReportOptionsValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_ForDefaultFakeOptions()
    {
        var options = new FactoryReportOptions();

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.Equal(ValidateOptionsResult.Success, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SqlServer")]
    [InlineData("memory")]
    public void Validate_Fails_ForInvalidDataMode(string dataMode)
    {
        var options = new FactoryReportOptions { DataMode = dataMode };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3601)]
    public void Validate_Fails_ForOutOfRangeHeartbeat(int seconds)
    {
        var options = new FactoryReportOptions
        {
            DataMode = "Fake",
            Worker = new WorkerRuntimeOptions { HeartbeatIntervalSeconds = seconds }
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("Fake")]
    [InlineData("fake")]
    [InlineData("Oracle")]
    public void Validate_Succeeds_ForKnownDataModes(string dataMode)
    {
        var options = new FactoryReportOptions { DataMode = dataMode };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SqlServer")]
    [InlineData("memory")]
    public void Validate_Fails_ForInvalidAccountStore(string accountStore)
    {
        var options = new FactoryReportOptions
        {
            DataMode = "Fake",
            Authentication = new AuthenticationOptions { AccountStore = accountStore }
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
    }

    [Theory]
    [InlineData("Fake")]
    [InlineData("Oracle")]
    public void Validate_Succeeds_ForKnownAccountStores(string accountStore)
    {
        var options = new FactoryReportOptions
        {
            DataMode = "Fake",
            Authentication = new AuthenticationOptions { AccountStore = accountStore }
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.False(result.Failed);
    }
}
