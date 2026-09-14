using System.Net;
using System.Text.Json;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.IntegrationTests;

public class WorkOrderProgressReportApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public WorkOrderProgressReportApiTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string BuildUrl(
        long? factoryId = DeterministicFakeFixture.FactoryDemo1Id,
        long? workshopId = null,
        long? productionLineId = null,
        string? productCode = null,
        string? workOrderCode = null,
        string? status = null,
        string? plannedFinishFrom = null,
        string? plannedFinishTo = null)
    {
        var parts = new List<string>();
        if (factoryId is not null)
        {
            parts.Add($"factoryId={factoryId}");
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

        if (workOrderCode is not null)
        {
            parts.Add($"workOrderCode={Uri.EscapeDataString(workOrderCode)}");
        }

        if (status is not null)
        {
            parts.Add($"status={Uri.EscapeDataString(status)}");
        }

        if (plannedFinishFrom is not null)
        {
            parts.Add($"plannedFinishFrom={plannedFinishFrom}");
        }

        if (plannedFinishTo is not null)
        {
            parts.Add($"plannedFinishTo={plannedFinishTo}");
        }

        return "/api/v1/reports/work-order-progress?" + string.Join("&", parts);
    }

    [Fact]
    public async Task Get_ReturnsRows_WithMeta_AndFakeMode()
    {
        var response = await _client.GetAsync(BuildUrl(
            workOrderCode: DeterministicFakeFixture.WorkOrderNormal));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var meta = root.GetProperty("meta");
        Assert.Equal("work_order_progress", meta.GetProperty("reportCode").GetString());
        Assert.Equal("Fake", meta.GetProperty("dataAccessMode").GetString());
        Assert.True(meta.GetProperty("isFake").GetBoolean());
        Assert.Contains("Fake 测试规则", meta.GetProperty("overdueDisclaimer").GetString());

        var row = Assert.Single(root.GetProperty("rows").EnumerateArray());
        Assert.Equal(DeterministicFakeFixture.WorkOrderNormal, row.GetProperty("workOrderCode").GetString());
        Assert.Equal(120m, row.GetProperty("plannedQuantity").GetDecimal());
        Assert.Equal(100m, row.GetProperty("actualQuantity").GetDecimal());
        Assert.False(row.GetProperty("isCompleted").GetBoolean());
        Assert.False(row.GetProperty("isOverdue").GetBoolean());
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
            Assert.NotEqual(
                DeterministicFakeFixture.WorkOrderFactory2,
                row.GetProperty("workOrderCode").GetString());
        }

        var f2Rows = d2.RootElement.GetProperty("rows");
        Assert.True(f2Rows.GetArrayLength() >= 1);
        Assert.Equal(
            DeterministicFakeFixture.WorkOrderFactory2,
            f2Rows[0].GetProperty("workOrderCode").GetString());
    }

    [Fact]
    public async Task Get_Filters_Status_WorkOrder_Product_Org_DateRange()
    {
        var byStatus = await _client.GetAsync(BuildUrl(status: DeterministicFakeFixture.StatusCompleted));
        using var sDoc = JsonDocument.Parse(await byStatus.Content.ReadAsStringAsync());
        foreach (var row in sDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(DeterministicFakeFixture.StatusCompleted, row.GetProperty("status").GetString());
        }

        var byWo = await _client.GetAsync(BuildUrl(workOrderCode: DeterministicFakeFixture.WorkOrderWorkshopB));
        using var wDoc = JsonDocument.Parse(await byWo.Content.ReadAsStringAsync());
        Assert.Equal(1, wDoc.RootElement.GetProperty("rows").GetArrayLength());
        Assert.Equal(
            DeterministicFakeFixture.WorkOrderWorkshopB,
            wDoc.RootElement.GetProperty("rows")[0].GetProperty("workOrderCode").GetString());

        var byProduct = await _client.GetAsync(BuildUrl(productCode: DeterministicFakeFixture.ProductPlanZero));
        using var pDoc = JsonDocument.Parse(await byProduct.Content.ReadAsStringAsync());
        foreach (var row in pDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(DeterministicFakeFixture.ProductPlanZero, row.GetProperty("productCode").GetString());
        }

        var byWorkshop = await _client.GetAsync(BuildUrl(workshopId: DeterministicFakeFixture.WorkshopBId));
        using var wsDoc = JsonDocument.Parse(await byWorkshop.Content.ReadAsStringAsync());
        foreach (var row in wsDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.Equal(DeterministicFakeFixture.WorkshopBId, row.GetProperty("workshopId").GetInt64());
        }

        var byDate = await _client.GetAsync(BuildUrl(
            plannedFinishFrom: "2026-03-11",
            plannedFinishTo: "2026-03-11"));
        using var dDoc = JsonDocument.Parse(await byDate.Content.ReadAsStringAsync());
        Assert.True(dDoc.RootElement.GetProperty("rows").GetArrayLength() >= 1);
        foreach (var row in dDoc.RootElement.GetProperty("rows").EnumerateArray())
        {
            var finish = row.GetProperty("plannedFinishUtc").GetDateTimeOffset();
            Assert.Equal(new DateOnly(2026, 3, 11), DateOnly.FromDateTime(finish.UtcDateTime));
        }
    }

    [Fact]
    public async Task Get_PlanZero_CompletionRateIsNull()
    {
        var response = await _client.GetAsync(BuildUrl(
            workOrderCode: DeterministicFakeFixture.WorkOrderPlanZero));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = doc.RootElement.GetProperty("rows")[0];
        Assert.Equal(0m, row.GetProperty("plannedQuantity").GetDecimal());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("completionRate").ValueKind);
    }

    [Fact]
    public async Task Get_OverdueRules_ClosedNotOverdue_OpenPastFinishIsOverdue()
    {
        var closed = await _client.GetAsync(BuildUrl(workOrderCode: DeterministicFakeFixture.WorkOrderClosed));
        using var cDoc = JsonDocument.Parse(await closed.Content.ReadAsStringAsync());
        var closedRow = cDoc.RootElement.GetProperty("rows")[0];
        Assert.True(closedRow.GetProperty("isCompleted").GetBoolean());
        Assert.False(closedRow.GetProperty("isOverdue").GetBoolean());

        var overdue = await _client.GetAsync(BuildUrl(
            workOrderCode: DeterministicFakeFixture.WorkOrderOverdueOpen));
        using var oDoc = JsonDocument.Parse(await overdue.Content.ReadAsStringAsync());
        var overdueRow = oDoc.RootElement.GetProperty("rows")[0];
        Assert.False(overdueRow.GetProperty("isCompleted").GetBoolean());
        Assert.True(overdueRow.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task Get_MissingFactoryId_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync(BuildUrl(factoryId: null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, doc.RootElement.GetProperty("status").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out _));
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("FactoryId", out _));
    }

    [Fact]
    public async Task Get_InvalidPlannedFinishRange_ReturnsProblemDetails()
    {
        var response = await _client.GetAsync(BuildUrl(
            plannedFinishFrom: "2026-03-12",
            plannedFinishTo: "2026-03-10"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("PlannedFinishDateRange", out _));
    }
}
