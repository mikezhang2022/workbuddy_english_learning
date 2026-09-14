using FactoryReport.Application.Reporting.ProductionDaily;
using FactoryReport.Client.Services.Display;

namespace FactoryReport.UnitTests.Client;

public class RatioDisplayFormatterTests
{
    [Fact]
    public void FormatPercent_Null_ShowsEmDash()
    {
        Assert.Equal("—", RatioDisplayFormatter.FormatPercent(null));
    }

    [Fact]
    public void FormatPercent_Null_DoesNotShowZeroPercent()
    {
        var text = RatioDisplayFormatter.FormatPercent(null);
        Assert.DoesNotContain("0%", text);
        Assert.DoesNotContain("0.00%", text);
    }

    [Fact]
    public void FormatPercent_Value_ShowsPercent()
    {
        Assert.Equal("90%", RatioDisplayFormatter.FormatPercent(0.9m));
        Assert.Equal("12.5%", RatioDisplayFormatter.FormatPercent(0.125m));
    }
}

public class ProductionDailyDisplaySummaryTests
{
    [Fact]
    public void FromRows_SumsQuantities_DoesNotComputeYield()
    {
        var summary = ProductionDailyDisplaySummary.FromRows(
        [
            new ProductionDailyReportRow
            {
                ActualQuantity = 100,
                GoodQuantity = 90,
                DefectQuantity = 5,
                ScrapQuantity = 3,
                ReworkQuantity = 2,
                InspectionQuantity = 100,
                YieldRate = 0.9m
            },
            new ProductionDailyReportRow
            {
                ActualQuantity = 40,
                GoodQuantity = 40,
                DefectQuantity = 0,
                ScrapQuantity = 0,
                ReworkQuantity = 0,
                InspectionQuantity = 0,
                YieldRate = null
            }
        ]);

        Assert.Equal(2, summary.RowCount);
        Assert.Equal(140, summary.ActualQuantity);
        Assert.Equal(130, summary.GoodQuantity);
        Assert.Equal(5, summary.DefectQuantity);
        Assert.Equal(3, summary.ScrapQuantity);
        Assert.Equal(2, summary.ReworkQuantity);
        Assert.Equal(100, summary.InspectionQuantity);
    }
}
