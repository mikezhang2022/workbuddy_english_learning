using FactoryReport.Client.Services.Api;

namespace FactoryReport.UnitTests.Client;

public class ApiProblemDetailsParserTests
{
    [Fact]
    public void TryParse_ReadsStatusDetailAndCorrelationId()
    {
        const string json = """
            {
              "status": 403,
              "title": "Forbidden",
              "detail": "Out of scope",
              "correlationId": "abc123"
            }
            """;

        var problem = ApiProblemDetailsParser.TryParse(json);

        Assert.NotNull(problem);
        Assert.Equal(403, problem!.Status);
        Assert.Equal("Out of scope", problem.Detail);
        Assert.Equal("abc123", problem.CorrelationId);
        Assert.Contains("无权访问", problem.UserFacingMessage);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsNull()
    {
        Assert.Null(ApiProblemDetailsParser.TryParse("{ not json"));
    }

    [Fact]
    public void NetworkError_HasChineseDetail()
    {
        var problem = ApiProblemDetailsParser.NetworkError("cid-1");
        Assert.Equal("cid-1", problem.CorrelationId);
        Assert.Contains("网络", problem.Detail);
    }
}
