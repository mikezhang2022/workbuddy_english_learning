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
}
