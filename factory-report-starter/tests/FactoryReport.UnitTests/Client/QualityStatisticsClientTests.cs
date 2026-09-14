using FactoryReport.Application.Reporting.QualityStatistics;
using FactoryReport.Client.Services.Api;
using FactoryReport.Client.Services.Display;

namespace FactoryReport.UnitTests.Client;

public class QualityStatisticsClientPathTests
{
    [Fact]
    public void BuildQualityStatisticsPath_IncludesRequiredAndOptionalFilters()
    {
        var path = ReportsApiClient.BuildQualityStatisticsPath(new QualityStatisticsClientQuery
        {
            FactoryId = 1,
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 11),
            WorkshopId = 10,
            ProductionLineId = 101,
            ProductCode = "PROD-NORMAL"
        });

        Assert.StartsWith("/api/v1/reports/quality-statistics?", path);
        Assert.Contains("factoryId=1", path);
        Assert.Contains("startDate=2026-03-10", path);
        Assert.Contains("endDate=2026-03-11", path);
        Assert.Contains("workshopId=10", path);
        Assert.Contains("productionLineId=101", path);
        Assert.Contains("productCode=PROD-NORMAL", path);
        Assert.DoesNotContain("workOrderCode=", path);
    }

    [Fact]
    public void BuildQualityStatisticsPath_OmitsEmptyOptionalFilters()
    {
        var path = ReportsApiClient.BuildQualityStatisticsPath(new QualityStatisticsClientQuery
        {
            FactoryId = 2,
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 10)
        });

        Assert.Contains("factoryId=2", path);
        Assert.Contains("startDate=2026-03-10", path);
        Assert.Contains("endDate=2026-03-10", path);
        Assert.DoesNotContain("workshopId=", path);
        Assert.DoesNotContain("productionLineId=", path);
        Assert.DoesNotContain("productCode=", path);
    }

    [Fact]
    public void BuildQualityStatisticsPath_RequiresUserDates_DoesNotInventToday()
    {
        var path = ReportsApiClient.BuildQualityStatisticsPath(new QualityStatisticsClientQuery
        {
            FactoryId = 1,
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 12),
            ProductCode = "PROD-ZERO-INSP"
        });

        Assert.Contains("startDate=2026-03-10", path);
        Assert.Contains("endDate=2026-03-12", path);
        Assert.DoesNotContain("DateTime.UtcNow", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DateTime.Today", path, StringComparison.OrdinalIgnoreCase);
    }
}

public class QualityStatisticsRateDisplayTests
{
    [Fact]
    public void YieldRate_Null_WhenInspectionIsZero_ShowsEmDash()
    {
        // API：InspectionQuantity = 0 → YieldRate = null；页面用同一 RatioDisplayFormatter。
        decimal? yieldRateWhenInspectionIsZero = null;
        Assert.Equal("—", RatioDisplayFormatter.FormatPercent(yieldRateWhenInspectionIsZero));
        Assert.DoesNotContain("0%", RatioDisplayFormatter.FormatPercent(yieldRateWhenInspectionIsZero));
    }

    [Fact]
    public void DefectRate_Null_WhenInspectionIsZero_ShowsEmDash()
    {
        decimal? defectRateWhenInspectionIsZero = null;
        Assert.Equal("—", RatioDisplayFormatter.FormatPercent(defectRateWhenInspectionIsZero));
        Assert.DoesNotContain("0%", RatioDisplayFormatter.FormatPercent(defectRateWhenInspectionIsZero));
    }

    [Fact]
    public void Rates_Value_ShowsPercent_WithoutRecalculation()
    {
        Assert.Equal("90%", RatioDisplayFormatter.FormatPercent(0.9m));
        Assert.Equal("5%", RatioDisplayFormatter.FormatPercent(0.05m));
    }
}

public class QualityStatisticsDisplaySummaryTests
{
    [Fact]
    public void FromRows_SumsQuantities_Separately_DoesNotMergeScrapReworkIntoDefect()
    {
        var summary = QualityStatisticsDisplaySummary.FromRows(
        [
            new QualityStatisticsReportRow
            {
                InspectionQuantity = 100,
                GoodQuantity = 90,
                DefectQuantity = 5,
                ScrapQuantity = 3,
                ReworkQuantity = 2,
                YieldRate = 0.9m,
                DefectRate = 0.05m
            },
            new QualityStatisticsReportRow
            {
                InspectionQuantity = 0,
                GoodQuantity = 0,
                DefectQuantity = 0,
                ScrapQuantity = 1,
                ReworkQuantity = 1,
                YieldRate = null,
                DefectRate = null
            }
        ]);

        Assert.Equal(2, summary.RowCount);
        Assert.Equal(100, summary.InspectionQuantity);
        Assert.Equal(90, summary.GoodQuantity);
        Assert.Equal(5, summary.DefectQuantity);
        Assert.Equal(4, summary.ScrapQuantity);
        Assert.Equal(3, summary.ReworkQuantity);
        // 报废/返工合计不得并入不良展示合计
        Assert.NotEqual(5 + 4 + 3, summary.DefectQuantity);
    }

    [Fact]
    public void FromRows_Empty_ReturnsZeros()
    {
        var summary = QualityStatisticsDisplaySummary.FromRows([]);
        Assert.Equal(0, summary.RowCount);
        Assert.Equal(0, summary.InspectionQuantity);
        Assert.Equal(0, summary.DefectQuantity);
    }
}

public class QualityMetricsDisplayTests
{
    [Fact]
    public void FakeRateHint_MentionsFakeQualityRates()
    {
        Assert.Contains("质量比率", QualityMetricsDisplay.FakeRateHint);
        Assert.Contains("Fake", QualityMetricsDisplay.FakeRateHint);
        Assert.Contains("现场 MES 接入前须确认", QualityMetricsDisplay.FakeRateHint);
    }

    [Fact]
    public void QuantityColumnsHint_SaysNotMergeScrapRework()
    {
        Assert.Contains("分列", QualityMetricsDisplay.QuantityColumnsHint);
        Assert.Contains("不把报废或返工并入不良", QualityMetricsDisplay.QuantityColumnsHint);
    }
}
