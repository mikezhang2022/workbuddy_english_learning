using FactoryReport.Application.Security.DataScope;
using FactoryReport.Client.Services.Display;

namespace FactoryReport.UnitTests.Client;

public class ReportDataScopeOptionsTests
{
    [Fact]
    public void GetFactoryOptions_SingleGrant_AutoSelectsThatFactory()
    {
        var scope = new DataScopeSummary
        {
            IsGlobal = false,
            Grants = [new DataScopeGrantSummary { FactoryId = 1 }]
        };

        var factories = ReportDataScopeOptions.GetFactoryOptions(scope);
        Assert.Single(factories);
        Assert.Equal(1, factories[0].Id);
        Assert.Equal(1, ReportDataScopeOptions.TryAutoSelectFactory(scope));
    }

    [Fact]
    public void GetFactoryOptions_Global_RequiresExplicitSelection()
    {
        var scope = new DataScopeSummary { IsGlobal = true, Grants = [] };

        var factories = ReportDataScopeOptions.GetFactoryOptions(scope);
        Assert.True(factories.Count >= 2);
        Assert.Null(ReportDataScopeOptions.TryAutoSelectFactory(scope));
    }

    [Fact]
    public void GetWorkshopOptions_DoesNotExposeUnauthorizedWorkshop()
    {
        var scope = new DataScopeSummary
        {
            IsGlobal = false,
            Grants =
            [
                new DataScopeGrantSummary { FactoryId = 1, WorkshopId = 10 }
            ]
        };

        var workshops = ReportDataScopeOptions.GetWorkshopOptions(scope, 1);
        Assert.Single(workshops);
        Assert.Equal(10, workshops[0].Id);
        Assert.DoesNotContain(workshops, w => w.Id == 20);
        Assert.False(ReportDataScopeOptions.AllowsWorkshop(scope, 1, 20));
    }

    [Fact]
    public void GetProductionLineOptions_LineLevelGrant_OnlyAuthorizedLine()
    {
        var scope = new DataScopeSummary
        {
            IsGlobal = false,
            Grants =
            [
                new DataScopeGrantSummary
                {
                    FactoryId = 1,
                    WorkshopId = 20,
                    ProductionLineId = 201
                }
            ]
        };

        var lines = ReportDataScopeOptions.GetProductionLineOptions(scope, 1, 20);
        Assert.Single(lines);
        Assert.Equal(201, lines[0].Id);
        Assert.False(ReportDataScopeOptions.AllowsProductionLine(scope, 1, 20, 101));
    }

    [Fact]
    public void GetFactoryOptions_NullScope_Empty()
    {
        Assert.Empty(ReportDataScopeOptions.GetFactoryOptions(null));
        Assert.Null(ReportDataScopeOptions.TryAutoSelectFactory(null));
    }
}
