using FactoryReport.Application.Security.DataScope;
using FactoryReport.Client.Services.Display;

namespace FactoryReport.UnitTests.Client;

public class DataScopeDisplayFormatterTests
{
    [Fact]
    public void FormatSummaryLines_Global_ReturnsGlobalLabel()
    {
        var lines = DataScopeDisplayFormatter.FormatSummaryLines(new DataScopeSummary
        {
            IsGlobal = true,
            Grants = []
        });

        Assert.Single(lines);
        Assert.Contains("全局", lines[0]);
    }

    [Fact]
    public void FormatSummaryLines_Grant_FormatsHierarchy()
    {
        var lines = DataScopeDisplayFormatter.FormatSummaryLines(new DataScopeSummary
        {
            IsGlobal = false,
            Grants =
            [
                new DataScopeGrantSummary { FactoryId = 1, WorkshopId = 10, ProductionLineId = null }
            ]
        });

        Assert.Contains(lines, l => l.Contains("工厂 1") && l.Contains("车间 10"));
    }
}
