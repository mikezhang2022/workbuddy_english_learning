using FactoryReport.Client.Services.Api;

namespace FactoryReport.UnitTests.Client;

public class ReportsApiClientPathTests
{
    [Fact]
    public void BuildProductionDailyPath_IncludesRequiredAndOptionalFilters()
    {
        var path = ReportsApiClient.BuildProductionDailyPath(new ProductionDailyClientQuery
        {
            FactoryId = 1,
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 11),
            WorkshopId = 10,
            ProductionLineId = 101,
            ProductCode = "PROD-NORMAL"
        });

        Assert.StartsWith("/api/v1/reports/production-daily?", path);
        Assert.Contains("factoryId=1", path);
        Assert.Contains("startDate=2026-03-10", path);
        Assert.Contains("endDate=2026-03-11", path);
        Assert.Contains("workshopId=10", path);
        Assert.Contains("productionLineId=101", path);
        Assert.Contains("productCode=PROD-NORMAL", path);
    }

    [Fact]
    public void BuildProductionDailyPath_OmitsEmptyOptionalFilters()
    {
        var path = ReportsApiClient.BuildProductionDailyPath(new ProductionDailyClientQuery
        {
            FactoryId = 2,
            StartDate = new DateOnly(2026, 3, 10),
            EndDate = new DateOnly(2026, 3, 10)
        });

        Assert.Contains("factoryId=2", path);
        Assert.DoesNotContain("workshopId=", path);
        Assert.DoesNotContain("productionLineId=", path);
        Assert.DoesNotContain("productCode=", path);
    }

    [Fact]
    public void BuildWorkOrderProgressPath_IncludesRequiredAndOptionalFilters()
    {
        var path = ReportsApiClient.BuildWorkOrderProgressPath(new WorkOrderProgressClientQuery
        {
            FactoryId = 1,
            WorkshopId = 10,
            ProductionLineId = 101,
            ProductCode = "PROD-ACTUAL-ONLY",
            WorkOrderCode = "WO-DEMO-OVERDUE",
            Status = "Open",
            PlannedFinishFrom = new DateOnly(2026, 3, 11),
            PlannedFinishTo = new DateOnly(2026, 3, 12)
        });

        Assert.StartsWith("/api/v1/reports/work-order-progress?", path);
        Assert.Contains("factoryId=1", path);
        Assert.Contains("workshopId=10", path);
        Assert.Contains("productionLineId=101", path);
        Assert.Contains("productCode=PROD-ACTUAL-ONLY", path);
        Assert.Contains("workOrderCode=WO-DEMO-OVERDUE", path);
        Assert.Contains("status=Open", path);
        Assert.Contains("plannedFinishFrom=2026-03-11", path);
        Assert.Contains("plannedFinishTo=2026-03-12", path);
    }

    [Fact]
    public void BuildWorkOrderProgressPath_OmitsEmptyOptionalFilters()
    {
        var path = ReportsApiClient.BuildWorkOrderProgressPath(new WorkOrderProgressClientQuery
        {
            FactoryId = 2
        });

        Assert.Contains("factoryId=2", path);
        Assert.DoesNotContain("workshopId=", path);
        Assert.DoesNotContain("productionLineId=", path);
        Assert.DoesNotContain("productCode=", path);
        Assert.DoesNotContain("workOrderCode=", path);
        Assert.DoesNotContain("status=", path);
        Assert.DoesNotContain("plannedFinishFrom=", path);
        Assert.DoesNotContain("plannedFinishTo=", path);
    }

    [Fact]
    public void BuildWorkOrderProgressPath_RequiresFactoryId_DoesNotInventDateFilter()
    {
        var path = ReportsApiClient.BuildWorkOrderProgressPath(new WorkOrderProgressClientQuery
        {
            FactoryId = 1,
            WorkOrderCode = "WO-DEMO-1001"
        });

        Assert.Contains("factoryId=1", path);
        Assert.Contains("workOrderCode=WO-DEMO-1001", path);
        Assert.DoesNotContain("plannedFinishFrom=", path);
        Assert.DoesNotContain("DateTime.UtcNow", path, StringComparison.OrdinalIgnoreCase);
    }
}

public class ApiRequestExceptionBehaviorTests
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
    }

    [Fact]
    public void OfflineMessage_IsDataRequiresNetwork()
    {
        Assert.Equal("数据需联网获取", ApiConstants.ReportOfflineMessage);
    }
}
