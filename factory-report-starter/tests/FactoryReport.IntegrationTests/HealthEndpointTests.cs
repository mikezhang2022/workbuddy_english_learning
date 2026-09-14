using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FactoryReport.IntegrationTests;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FactoryReport:DataMode"] = "Fake",
                ["FactoryReport:ExposeTestExceptionEndpoint"] = "true",
                ["FactoryReport:Worker:HeartbeatIntervalSeconds"] = "30"
            });
        });
    }
}

public class HealthEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk_WithFakeMode()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload!.Status);
        Assert.Equal("Fake", payload.Mode);
        Assert.True(payload.IsFake);
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthy_InFakeMode_WithoutExternalCalls()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());

        var checks = doc.RootElement.GetProperty("checks");
        Assert.True(checks.GetArrayLength() >= 1);
        var description = checks[0].GetProperty("description").GetString() ?? string.Empty;
        Assert.Contains("Fake", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no Oracle/MES", description, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class HealthPayload
    {
        public string? Status { get; set; }
        public string? Mode { get; set; }
        public bool IsFake { get; set; }
    }
}

public class CorrelationIdTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorrelationIdTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Response_IncludesGeneratedCorrelationId_WhenRequestHasNone()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        var correlationId = Assert.Single(values);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task Response_EchoesIncomingCorrelationId()
    {
        const string expected = "test-correlation-abc123";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", expected);

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-ID", out var values));
        Assert.Equal(expected, Assert.Single(values));
    }
}

public class ProblemDetailsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProblemDetailsTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UnhandledException_ReturnsProblemDetails_WithCorrelationId()
    {
        const string correlationId = "problem-details-corr-001";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/__test/exception");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.Equal(correlationId, root.GetProperty("correlationId").GetString());
        Assert.Contains("Intentional test exception", root.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task UnknownRoute_ReturnsProblemDetailsStatus()
    {
        var response = await _client.GetAsync("/definitely-missing-route-xyz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.True(
            mediaType is "application/problem+json" or "application/json",
            $"Unexpected media type: {mediaType}");
    }
}
