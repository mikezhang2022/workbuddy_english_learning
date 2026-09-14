using System.Net;
using System.Text.Json;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.IntegrationTests;

public class MonthlyProductionPlanReportApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MonthlyProductionPlanReportApiTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string BuildUrl(
        long? factoryId = DeterministicFakeFixture.FactoryDemo1Id,
        string? planMonth = DeterministicFakeFixture.PlanYearMonth202603,
        long? workshopId = null,
        long? productionLineId = null,
        string? productCode = null)
    {
        var parts = new List<string>();
        if (factoryId is not null)
        {
            parts.Add($"factoryId={factoryId}");
        }

        if (planMonth is not null)
        {
            parts.Add($"planMonth={Uri.EscapeDataString(planMonth)}");
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

        return "/api/v1/reports/monthly-production-plan?" + string.Join("&", parts);
    }

    [Fact]
    public async Task Get_ByFactoryIdAndPlanMonth_Ok()
    {
        var response = await _client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.True(rows.GetArrayLength() > 0);
        foreach (var row in rows.EnumerateArray())
        {
            var planDate = row.GetProperty("planDate").GetString()!;
            Assert.StartsWith("2026-03-", planDate);
            Assert.Equal(1, row.GetProperty("factoryId").GetInt64());
            Assert.NotEqual(
                DeterministicFakeFixture.ProductDraftOnly,
                row.GetProperty("productCode").GetString());
        }
    }

    [Fact]
    public async Task Get_PlanDates_BelongToMonth()
    {
        var response = await _client.GetAsync(BuildUrl(planMonth: "2026-03"));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.StartsWith("2026-03-", row.GetProperty("planDate").GetString());
        }

        var empty = await _client.GetAsync(BuildUrl(planMonth: "2025-12"));
        using var emptyDoc = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
        Assert.Equal(0, emptyDoc.RootElement.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task Get_Filters_WorkshopLineProduct()
    {
        var byWorkshop = await _client.GetAsync(BuildUrl(workshopId: DeterministicFakeFixture.WorkshopBId));
        using var wDoc = JsonDocument.Parse(await byWorkshop.Content.ReadAsStringAsync());
        foreach (var wRow in wDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(DeterministicFakeFixture.WorkshopBId, wRow.GetProperty("workshopId").GetInt64());
        }

        var byLine = await _client.GetAsync(BuildUrl(productionLineId: DeterministicFakeFixture.LineA2Id));
        using var lDoc = JsonDocument.Parse(await byLine.Content.ReadAsStringAsync());
        foreach (var lRow in lDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(DeterministicFakeFixture.LineA2Id, lRow.GetProperty("productionLineId").GetInt64());
        }

        var byProduct = await _client.GetAsync(BuildUrl(productCode: DeterministicFakeFixture.ProductDay2));
        using var pDoc = JsonDocument.Parse(await byProduct.Content.ReadAsStringAsync());
        var productRow = Assert.Single(pDoc.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(DeterministicFakeFixture.ProductDay2, productRow.GetProperty("productCode").GetString());
        Assert.Equal(35, productRow.GetProperty("planQuantity").GetDecimal());
    }

    [Fact]
    public async Task Get_IsolatesFactories()
    {
        var f1 = await _client.GetAsync(BuildUrl(factoryId: 1));
        var f2 = await _client.GetAsync(BuildUrl(factoryId: 2));
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
        Assert.Equal(1, f2Rows.GetArrayLength());
        Assert.Equal("PROD-F2", f2Rows[0].GetProperty("productCode").GetString());
    }

    [Fact]
    public async Task Get_DraftVersionRows_NotReturned()
    {
        var response = await _client.GetAsync(BuildUrl(
            productCode: DeterministicFakeFixture.ProductDraftOnly));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task Get_RowsMatchFixture_NoMonthlyAverage()
    {
        var snapshot = DeterministicFakeFixture.Create();
        var expected = snapshot.DailyPlanLines
            .Where(p => p.FactoryId == 1
                        && p.PlanVersionId == DeterministicFakeFixture.DatasetVersionFactory1Id
                        && p.PlanDate.Year == 2026
                        && p.PlanDate.Month == 3)
            .ToList();

        var response = await _client.GetAsync(BuildUrl());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(expected.Count, rows.Count);

        foreach (var fixtureLine in expected)
        {
            Assert.Contains(rows, r =>
                r.GetProperty("planDate").GetString() == fixtureLine.PlanDate.ToString("yyyy-MM-dd")
                && r.GetProperty("productCode").GetString() == fixtureLine.ProductCode
                && r.GetProperty("planQuantity").GetDecimal() == fixtureLine.PlanQuantity
                && r.GetProperty("workshopId").GetInt64() == fixtureLine.WorkshopId);
        }
    }

    [Fact]
    public async Task Get_InvalidPlanMonth_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync(BuildUrl(planMonth: "2026/03"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out _));
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("PlanMonth", out _));
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
        var response = await _client.GetAsync(BuildUrl());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var meta = doc.RootElement.GetProperty("meta");
        Assert.Equal("monthly_production_plan", meta.GetProperty("reportCode").GetString());
        Assert.Equal("Fake", meta.GetProperty("dataAccessMode").GetString());
        Assert.True(meta.GetProperty("isFake").GetBoolean());
        Assert.Contains("FAKE DATA VERSION BEHAVIOR", meta.GetProperty("fakeVersionRuleNote").GetString());
        Assert.Equal(
            DeterministicFakeFixture.DatasetVersionFactory1Id.ToString(),
            meta.GetProperty("activePlanVersionId").GetString());
    }
}
