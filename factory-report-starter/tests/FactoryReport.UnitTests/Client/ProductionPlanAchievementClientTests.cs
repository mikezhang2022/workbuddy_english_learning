using FactoryReport.Application.Reporting.ProductionPlanAchievement;
using FactoryReport.Client.Services.Api;
using FactoryReport.Client.Services.Display;

namespace FactoryReport.UnitTests.Client;

public class ProductionPlanAchievementClientPathTests
{
    [Fact]
    public void BuildProductionPlanAchievementPath_IncludesRequiredAndOptionalFilters()
    {
        var path = ReportsApiClient.BuildProductionPlanAchievementPath(new ProductionPlanAchievementClientQuery
        {
            FactoryId = 1,
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 11),
            WorkshopId = 10,
            ProductionLineId = 101,
            ProductCode = "PROD-NORMAL"
        });

        Assert.StartsWith("/api/v1/reports/production-plan-achievement?", path);
        Assert.Contains("factoryId=1", path);
        Assert.Contains("startDate=2026-03-10", path);
        Assert.Contains("endDate=2026-03-11", path);
        Assert.Contains("workshopId=10", path);
        Assert.Contains("productionLineId=101", path);
        Assert.Contains("productCode=PROD-NORMAL", path);
    }

    [Fact]
    public void BuildProductionPlanAchievementPath_OmitsEmptyOptionalFilters()
    {
        var path = ReportsApiClient.BuildProductionPlanAchievementPath(new ProductionPlanAchievementClientQuery
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
    public void BuildProductionPlanAchievementPath_RequiresUserDates_DoesNotInventToday()
    {
        var path = ReportsApiClient.BuildProductionPlanAchievementPath(new ProductionPlanAchievementClientQuery
        {
            FactoryId = 1,
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 12),
            ProductCode = "PROD-PLAN-ONLY"
        });

        Assert.Contains("startDate=2026-03-10", path);
        Assert.Contains("endDate=2026-03-12", path);
        Assert.DoesNotContain("DateTime.UtcNow", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DateTime.Today", path, StringComparison.OrdinalIgnoreCase);
    }
}

public class PlanAchievementStatusDisplayTests
{
    [Theory]
    [InlineData(PlanAchievementStatusDisplay.Calculated, "正常计算")]
    [InlineData(PlanAchievementStatusDisplay.PlanIsZero, "计划为 0")]
    [InlineData(PlanAchievementStatusDisplay.PlanNotConfigured, "未配置计划")]
    [InlineData(PlanAchievementStatusDisplay.MissingActual, "有计划但无实际")]
    public void GetChineseLabel_FourStatuses(string status, string expected)
    {
        Assert.Equal(expected, PlanAchievementStatusDisplay.GetChineseLabel(status));
    }

    [Fact]
    public void FormatWithRaw_KeepsOriginalStatusForDiagnostics()
    {
        Assert.Equal(
            "未配置计划（PlanNotConfigured）",
            PlanAchievementStatusDisplay.FormatWithRaw(PlanAchievementStatusDisplay.PlanNotConfigured));
        Assert.Equal(
            "正常计算（Calculated）",
            PlanAchievementStatusDisplay.FormatWithRaw(PlanAchievementStatusDisplay.Calculated));
        Assert.Equal(
            "计划为 0（PlanIsZero）",
            PlanAchievementStatusDisplay.FormatWithRaw(PlanAchievementStatusDisplay.PlanIsZero));
        Assert.Equal(
            "有计划但无实际（MissingActual）",
            PlanAchievementStatusDisplay.FormatWithRaw(PlanAchievementStatusDisplay.MissingActual));
    }

    [Fact]
    public void AchievementRate_Null_ShowsEmDash_NotZeroPercent()
    {
        // PlanIsZero / PlanNotConfigured：AchievementRate = null
        Assert.Equal("—", PlanAchievementStatusDisplay.FormatAchievementRate(null));
        Assert.DoesNotContain("0%", PlanAchievementStatusDisplay.FormatAchievementRate(null));
    }

    [Fact]
    public void PlanNotConfigured_NeverShowsZeroPercent()
    {
        // API：PlanNotConfigured → AchievementRate = null；页面不得显示 0%
        decimal? rateForPlanNotConfigured = null;
        var display = PlanAchievementStatusDisplay.FormatAchievementRate(rateForPlanNotConfigured);
        Assert.Equal("—", display);
        Assert.DoesNotContain("0%", display);
        Assert.Equal("未配置计划", PlanAchievementStatusDisplay.GetChineseLabel("PlanNotConfigured"));
    }

    [Fact]
    public void MissingActual_ShowsActualZero_AndZeroPercent()
    {
        // API：MissingActual → ActualQuantity = 0, AchievementRate = 0
        var row = new ProductionPlanAchievementReportRow
        {
            PlanQuantity = 80,
            ActualQuantity = 0,
            AchievementRate = 0m,
            PlanStatus = PlanAchievementStatusDisplay.MissingActual
        };

        Assert.Equal(0, row.ActualQuantity);
        Assert.Equal("0%", PlanAchievementStatusDisplay.FormatAchievementRate(row.AchievementRate));
        Assert.Equal("有计划但无实际", PlanAchievementStatusDisplay.GetChineseLabel(row.PlanStatus));
    }

    [Fact]
    public void Calculated_ShowsPercent_WithoutRecalculation()
    {
        Assert.Equal("83.33%", PlanAchievementStatusDisplay.FormatAchievementRate(0.8333333333333334m));
    }

    [Fact]
    public void PlanQuantity_Null_ShowsEmDash()
    {
        Assert.Equal("—", PlanAchievementStatusDisplay.FormatPlanQuantity(null));
        Assert.Equal("120", PlanAchievementStatusDisplay.FormatPlanQuantity(120m));
        Assert.Equal("0", PlanAchievementStatusDisplay.FormatPlanQuantity(0m));
    }
}

public class ProductionPlanAchievementDisplaySummaryTests
{
    [Fact]
    public void FromRows_SumsPlanAndActual_SkipsNullPlanQuantity()
    {
        var summary = ProductionPlanAchievementDisplaySummary.FromRows(
        [
            new ProductionPlanAchievementReportRow
            {
                PlanQuantity = 120,
                ActualQuantity = 100,
                AchievementRate = 100m / 120m,
                PlanStatus = PlanAchievementStatusDisplay.Calculated
            },
            new ProductionPlanAchievementReportRow
            {
                PlanQuantity = null,
                ActualQuantity = 55,
                AchievementRate = null,
                PlanStatus = PlanAchievementStatusDisplay.PlanNotConfigured
            },
            new ProductionPlanAchievementReportRow
            {
                PlanQuantity = 80,
                ActualQuantity = 0,
                AchievementRate = 0m,
                PlanStatus = PlanAchievementStatusDisplay.MissingActual
            }
        ]);

        Assert.Equal(3, summary.RowCount);
        Assert.Equal(200, summary.PlanQuantity); // 120 + 80；null 不计
        Assert.Equal(155, summary.ActualQuantity); // 100 + 55 + 0
    }

    [Fact]
    public void FromRows_Empty_ReturnsZeros()
    {
        var summary = ProductionPlanAchievementDisplaySummary.FromRows([]);
        Assert.Equal(0, summary.RowCount);
        Assert.Equal(0, summary.PlanQuantity);
        Assert.Equal(0, summary.ActualQuantity);
    }
}

public class ProductionPlanAchievementErrorSemanticsTests
{
    [Fact]
    public void Unauthorized_IsUnauthorizedTrue()
    {
        var ex = new ApiRequestException(new ApiProblemDetails { Status = 401, Detail = "x" });
        Assert.True(ex.IsUnauthorized);
        Assert.False(ex.IsForbidden);
    }

    [Fact]
    public void Forbidden_UsesScopeMessage()
    {
        var problem = new ApiProblemDetails { Status = 403, Detail = "server detail" };
        Assert.Equal(ApiConstants.ForbiddenScopeMessage, problem.UserFacingMessage);
        var ex = new ApiRequestException(problem);
        Assert.True(ex.IsForbidden);
    }

    [Fact]
    public void NetworkError_HasNullStatus_ForOfflineHandling()
    {
        var problem = ApiProblemDetailsParser.NetworkError();
        Assert.Null(problem.Status);
        Assert.Contains("网络", problem.Detail);
        Assert.Equal("数据需联网获取", ApiConstants.ReportOfflineMessage);
    }
}
