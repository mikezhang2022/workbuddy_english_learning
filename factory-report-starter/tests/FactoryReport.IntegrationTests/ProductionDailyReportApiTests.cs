using System.Net;
using System.Text.Json;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.IntegrationTests;

public class ProductionDailyReportApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProductionDailyReportApiTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = AuthenticatedClientFactory.CreateSystemAdminClientAsync(factory).GetAwaiter().GetResult();
    }

    private static string BuildUrl(
        long? factoryId = DeterministicFakeFixture.FactoryDemo1Id,
        string? startDate = "2026-03-10",
        string? endDate = "2026-03-11",
        long? workshopId = null,
        long? productionLineId = null,
        string? productCode = null)
    {
        var parts = new List<string>();
        if (factoryId is not null)
        {
            parts.Add($"factoryId={factoryId}");
        }

        if (startDate is not null)
        {
            parts.Add($"startDate={startDate}");
        }

        if (endDate is not null)
        {
            parts.Add($"endDate={endDate}");
        }

        if (workshopId is not null)
        {
            parts.Add($"workshopId={workshopId}");
        }

        if (productionLineId is not null)
        {
            parts.Add($"productionLineId={productionLineId}");
        }

        if (productCode is not null)
        {
            parts.Add($"productCode={Uri.EscapeDataString(productCode)}");
        }

        return "/api/v1/reports/production-daily?" + string.Join("&", parts);
    }

    [Fact]
    public async Task Get_ReturnsAggregatedRows_WithMeta()
    {
        var response = await _client.GetAsync(BuildUrl(
            endDate: "2026-03-10",
            productCode: DeterministicFakeFixture.ProductNormal,
            workshopId: DeterministicFakeFixture.WorkshopAId,
            productionLineId: DeterministicFakeFixture.LineA1Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var meta = root.GetProperty("meta");
        Assert.Equal("production_daily", meta.GetProperty("reportCode").GetString());
        Assert.Equal("Fake", meta.GetProperty("dataAccessMode").GetString());
        Assert.True(meta.GetProperty("isFake").GetBoolean());
        Assert.Contains("Fake 测试口径", meta.GetProperty("yieldRateDisclaimer").GetString());

        var rows = root.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());
        var row = rows[0];
        Assert.Equal(100, row.GetProperty("actualQuantity").GetDecimal());
        Assert.Equal(90, row.GetProperty("goodQuantity").GetDecimal());
        Assert.Equal(100, row.GetProperty("inspectionQuantity").GetDecimal());
        Assert.Equal(0.9m, row.GetProperty("yieldRate").GetDecimal());
    }

    [Fact]
    public async Task Get_IsolatesFactories()
    {
        var f1 = await _client.GetAsync(BuildUrl(factoryId: 1, endDate: "2026-03-10"));
        var f2 = await _client.GetAsync(BuildUrl(factoryId: 2, endDate: "2026-03-10"));
        Assert.Equal(HttpStatusCode.OK, f1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, f2.StatusCode);

        using var d1 = JsonDocument.Parse(await f1.Content.ReadAsStringAsync());
        using var d2 = JsonDocument.Parse(await f2.Content.ReadAsStringAsync());

        foreach (var row in d1.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(1, row.GetProperty("factoryId").GetInt64());
            Assert.NotEqual("PROD-F2", row.GetProperty("productCode").GetString());
        }

        var f2Rows = d2.RootElement.GetProperty("rows");
        Assert.True(f2Rows.GetArrayLength() >= 1);
        Assert.Equal("PROD-F2", f2Rows[0].GetProperty("productCode").GetString());
    }

    [Fact]
    public async Task Get_DateRangeFilter_Works()
    {
        var day2 = await _client.GetAsync(BuildUrl(startDate: "2026-03-11", endDate: "2026-03-11"));
        using var doc = JsonDocument.Parse(await day2.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.True(rows.GetArrayLength() >= 1);
        foreach (var row in rows.EnumerateArray())
        {
            Assert.Equal("2026-03-11", row.GetProperty("productionDate").GetString());
        }
    }

    [Fact]
    public async Task Get_OptionalFilters_Apply()
    {
        var byWorkshop = await _client.GetAsync(BuildUrl(
            workshopId: DeterministicFakeFixture.WorkshopBId,
            endDate: "2026-03-10"));
        using var wDoc = JsonDocument.Parse(await byWorkshop.Content.ReadAsStringAsync());
        foreach (var row in wDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(DeterministicFakeFixture.WorkshopBId, row.GetProperty("workshopId").GetInt64());
        }

        var byLine = await _client.GetAsync(BuildUrl(
            productionLineId: DeterministicFakeFixture.LineA2Id,
            startDate: "2026-03-11",
            endDate: "2026-03-11"));
        using var lDoc = JsonDocument.Parse(await byLine.Content.ReadAsStringAsync());
        foreach (var row in lDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(DeterministicFakeFixture.LineA2Id, row.GetProperty("productionLineId").GetInt64());
        }

        var byProduct = await _client.GetAsync(BuildUrl(
            productCode: DeterministicFakeFixture.ProductPlanZero,
            endDate: "2026-03-10"));
        using var pDoc = JsonDocument.Parse(await byProduct.Content.ReadAsStringAsync());
        Assert.Equal(1, pDoc.RootElement.GetProperty("rows").GetArrayLength());
        Assert.Equal(
            DeterministicFakeFixture.ProductPlanZero,
            pDoc.RootElement.GetProperty("rows")[0].GetProperty("productCode").GetString());
    }

    [Fact]
    public async Task Get_InvalidDateRange_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync(BuildUrl(startDate: "2026-03-12", endDate: "2026-03-10"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out _));
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("DateRange", out _));
    }

    [Fact]
    public async Task Get_MissingFactoryId_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync(BuildUrl(factoryId: null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("FactoryId", out _));
    }

    [Fact]
    public async Task Get_Response_IncludesReportCode_AndFakeMode()
    {
        var response = await _client.GetAsync(BuildUrl(endDate: "2026-03-10"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var meta = doc.RootElement.GetProperty("meta");
        Assert.Equal("production_daily", meta.GetProperty("reportCode").GetString());
        Assert.Equal("Fake", meta.GetProperty("dataAccessMode").GetString());
        Assert.True(meta.GetProperty("isFake").GetBoolean());
    }
}
