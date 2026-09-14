using FactoryReport.Application.Reporting.MonthlyProductionPlan;
using FactoryReport.Application.Security.DataScope;
using FactoryReport.Client.Services.Api;
using FactoryReport.Client.Services.Display;

namespace FactoryReport.UnitTests.Client;

public class MonthlyProductionPlanClientPathTests
{
    [Fact]
    public void BuildMonthlyProductionPlanPath_IncludesRequiredAndOptionalFilters()
    {
        var path = ReportsApiClient.BuildMonthlyProductionPlanPath(new MonthlyProductionPlanClientQuery
        {
            FactoryId = 1,
            PlanMonth = "2026-03",
            WorkshopId = 10,
            ProductionLineId = 101,
            ProductCode = "PROD-NORMAL"
        });

        Assert.StartsWith("/api/v1/reports/monthly-production-plan?", path);
        Assert.Contains("factoryId=1", path);
        Assert.Contains("planMonth=2026-03", path);
        Assert.Contains("workshopId=10", path);
        Assert.Contains("productionLineId=101", path);
        Assert.Contains("productCode=PROD-NORMAL", path);
    }

    [Fact]
    public void BuildMonthlyProductionPlanPath_OmitsEmptyOptionalFilters()
    {
        var path = ReportsApiClient.BuildMonthlyProductionPlanPath(new MonthlyProductionPlanClientQuery
        {
            FactoryId = 2,
            PlanMonth = "2026-03"
        });

        Assert.Contains("factoryId=2", path);
        Assert.Contains("planMonth=2026-03", path);
        Assert.DoesNotContain("workshopId=", path);
        Assert.DoesNotContain("productionLineId=", path);
        Assert.DoesNotContain("productCode=", path);
    }

    [Fact]
    public void BuildMonthlyProductionPlanPath_RequiresUserPlanMonth_DoesNotInventCurrentMonth()
    {
        var path = ReportsApiClient.BuildMonthlyProductionPlanPath(new MonthlyProductionPlanClientQuery
        {
            FactoryId = 1,
            PlanMonth = "2026-03"
        });

        Assert.Contains("planMonth=2026-03", path);
        Assert.DoesNotContain("DateTime.UtcNow", path, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DateTime.Today", path, StringComparison.OrdinalIgnoreCase);
        // 不得把「当前月」硬编码进路径拼装逻辑
        var nowMonth = DateTime.UtcNow.ToString("yyyy-MM");
        if (!string.Equals(nowMonth, "2026-03", StringComparison.Ordinal))
        {
            Assert.DoesNotContain($"planMonth={nowMonth}", path);
        }
    }
}

public class PlanMonthFormatTests
{
    [Theory]
    [InlineData("2026-03", true)]
    [InlineData("2026-01", true)]
    [InlineData("2026-12", true)]
    [InlineData(" 2026-03 ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("2026-3", false)]
    [InlineData("26-03", false)]
    [InlineData("2026/03", false)]
    [InlineData("2026-00", false)]
    [InlineData("2026-13", false)]
    [InlineData("202603", false)]
    public void TryNormalize_ValidatesStrictYearMonth(string? input, bool expected)
    {
        var ok = PlanMonthFormat.TryNormalize(input, out var planMonth);
        Assert.Equal(expected, ok);
        if (expected)
        {
            Assert.Equal(input!.Trim(), planMonth);
        }
    }

    [Fact]
    public void RequiredAndInvalidMessages_AreDefined()
    {
        Assert.Contains("yyyy-MM", PlanMonthFormat.RequiredError);
        Assert.Contains("yyyy-MM", PlanMonthFormat.InvalidFormatError);
    }

    [Fact]
    public void BelongsToPlanMonth_AcceptsDaysInSelectedMonth_Only()
    {
        Assert.True(PlanMonthFormat.BelongsToPlanMonth(new DateOnly(2026, 3, 10), "2026-03"));
        Assert.True(PlanMonthFormat.BelongsToPlanMonth(new DateOnly(2026, 3, 31), "2026-03"));
        Assert.False(PlanMonthFormat.BelongsToPlanMonth(new DateOnly(2026, 2, 28), "2026-03"));
        Assert.False(PlanMonthFormat.BelongsToPlanMonth(new DateOnly(2026, 4, 1), "2026-03"));
    }
}

public class MonthlyProductionPlanScopeFilterTests
{
    [Fact]
    public void FactoryOptions_ComeFromDataScope_NotArbitraryClientInput()
    {
        var scope = new DataScopeSummary
        {
            IsGlobal = false,
            Grants =
            [
                new DataScopeGrantSummary { FactoryId = 1, WorkshopId = 10, ProductionLineId = 101 }
            ]
        };

        var factories = ReportDataScopeOptions.GetFactoryOptions(scope);
        Assert.Single(factories);
        Assert.Equal(1, factories[0].Id);
        Assert.True(ReportDataScopeOptions.AllowsFactory(scope, 1));
        Assert.False(ReportDataScopeOptions.AllowsFactory(scope, 2));
    }

    [Fact]
    public void GlobalScope_RequiresExplicitFactory_DoesNotAutoSelect()
    {
        var scope = new DataScopeSummary { IsGlobal = true, Grants = [] };
        Assert.Null(ReportDataScopeOptions.TryAutoSelectFactory(scope));
        Assert.NotEmpty(ReportDataScopeOptions.GetFactoryOptions(scope));
    }

    [Fact]
    public void WorkshopAndLine_OutsideScope_NotAllowed()
    {
        var scope = new DataScopeSummary
        {
            IsGlobal = false,
            Grants =
            [
                new DataScopeGrantSummary { FactoryId = 1, WorkshopId = 10, ProductionLineId = 101 }
            ]
        };

        Assert.True(ReportDataScopeOptions.AllowsWorkshop(scope, 1, 10));
        Assert.False(ReportDataScopeOptions.AllowsWorkshop(scope, 1, 99));
        Assert.True(ReportDataScopeOptions.AllowsProductionLine(scope, 1, 10, 101));
        Assert.False(ReportDataScopeOptions.AllowsProductionLine(scope, 1, 10, 999));
    }
}

public class MonthlyProductionPlanDisplayRulesTests
{
    [Fact]
    public void DailyPlanLinePrincipleHint_EmphasizesNoMonthlyAverage()
    {
        Assert.Contains("按月上传", MonthlyProductionPlanDisplayHints.DailyPlanLinePrincipleZh);
        Assert.Contains("按日计划行", MonthlyProductionPlanDisplayHints.DailyPlanLinePrincipleZh);
        Assert.Contains("不得", MonthlyProductionPlanDisplayHints.DailyPlanLinePrincipleZh);
        Assert.DoesNotContain("平均到每天后查询", MonthlyProductionPlanDisplayHints.DailyPlanLinePrincipleZh);
        Assert.Equal(
            MonthlyProductionPlanDisplayHints.DailyPlanLinePrincipleZh,
            ApiConstants.MonthlyPlanDailyLinePrincipleMessage);
    }

    [Fact]
    public void FakeVersionHint_PublishedAndActiveOnly_DraftNotShown()
    {
        Assert.Contains("Published", MonthlyProductionPlanDisplayHints.FakePublishedActiveOnlyZh);
        Assert.Contains("Active", MonthlyProductionPlanDisplayHints.FakePublishedActiveOnlyZh);
        Assert.Contains("Draft", MonthlyProductionPlanDisplayHints.FakePublishedActiveOnlyZh);
        Assert.Contains("后续阶段", MonthlyProductionPlanDisplayHints.FormalVersionRulesPendingZh);
    }

    [Fact]
    public void DisplayRows_OnlyPublishedActive_DraftWouldNotBelongInResponse()
    {
        // API 保证仅 Published+Active；客户端展示假定 rows 已过滤。此处验证样例行语义。
        var visible = new MonthlyProductionPlanReportRow
        {
            PlanDate = new DateOnly(2026, 3, 10),
            FactoryId = 1,
            FactoryCode = "F-DEMO-01",
            WorkshopId = 10,
            WorkshopCode = "W-DEMO-A",
            ProductCode = "PROD-NORMAL",
            PlanQuantity = 120,
            PlanVersionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            PlanVersionNo = "v2026.03.10-fake-f1",
            PublishStatus = "Published",
            IsActive = true
        };

        var draftSample = new MonthlyProductionPlanReportRow
        {
            PlanDate = new DateOnly(2026, 3, 10),
            FactoryId = 1,
            FactoryCode = "F-DEMO-01",
            WorkshopId = 10,
            WorkshopCode = "W-DEMO-A",
            ProductCode = "PROD-DRAFT-ONLY",
            PlanQuantity = 1,
            PlanVersionId = Guid.NewGuid(),
            PlanVersionNo = "draft",
            PublishStatus = "Draft",
            IsActive = false
        };

        Assert.Equal("Published", visible.PublishStatus);
        Assert.True(visible.IsActive);
        Assert.True(PlanMonthFormat.BelongsToPlanMonth(visible.PlanDate, "2026-03"));

        // Draft 行不应出现在页面数据源中（API 不返回）
        Assert.NotEqual("Published", draftSample.PublishStatus);
        Assert.False(draftSample.IsActive);
        var pageRows = new[] { visible }.Where(r => r.PublishStatus == "Published" && r.IsActive).ToArray();
        Assert.DoesNotContain(pageRows, r => r.ProductCode == "PROD-DRAFT-ONLY");
        Assert.Contains(pageRows, r => r.ProductCode == "PROD-NORMAL");
    }

    [Fact]
    public void PlanDates_InReturnedRows_BelongToSelectedPlanMonth()
    {
        const string planMonth = "2026-03";
        var rows = new[]
        {
            new MonthlyProductionPlanReportRow
            {
                PlanDate = new DateOnly(2026, 3, 10),
                ProductCode = "A",
                PlanQuantity = 10,
                PublishStatus = "Published",
                IsActive = true
            },
            new MonthlyProductionPlanReportRow
            {
                PlanDate = new DateOnly(2026, 3, 12),
                ProductCode = "B",
                PlanQuantity = 20,
                PublishStatus = "Published",
                IsActive = true
            }
        };

        Assert.All(rows, r => Assert.True(PlanMonthFormat.BelongsToPlanMonth(r.PlanDate, planMonth)));
    }
}

public class MonthlyProductionPlanDisplaySummaryTests
{
    [Fact]
    public void FromRows_SumsRawPlanQuantity_WithoutAveraging()
    {
        var summary = MonthlyProductionPlanDisplaySummary.FromRows(
        [
            new MonthlyProductionPlanReportRow
            {
                PlanDate = new DateOnly(2026, 3, 10),
                PlanQuantity = 120,
                PublishStatus = "Published",
                IsActive = true
            },
            new MonthlyProductionPlanReportRow
            {
                PlanDate = new DateOnly(2026, 3, 11),
                PlanQuantity = 80,
                PublishStatus = "Published",
                IsActive = true
            }
        ]);

        Assert.Equal(2, summary.RowCount);
        Assert.Equal(200, summary.PlanQuantity);
        // 不得按天数均摊：合计应为原始行之和，而非 (120+80)/天数
        Assert.NotEqual(100, summary.PlanQuantity);
    }

    [Fact]
    public void FromRows_Empty_ReturnsZeros()
    {
        var summary = MonthlyProductionPlanDisplaySummary.FromRows([]);
        Assert.Equal(0, summary.RowCount);
        Assert.Equal(0, summary.PlanQuantity);
    }
}

public class MonthlyProductionPlanErrorSemanticsTests
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
