using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Reporting.MonthlyProductionPlan;
using FactoryReport.Domain.Common;
using FactoryReport.Domain.Import;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.UnitTests.Reporting;

public class MonthlyProductionPlanReportServiceTests
{
    private sealed class FixedClock : IUtcClock
    {
        public UtcInstant UtcNow { get; } =
            UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));
    }

    private static MonthlyProductionPlanReportService CreateService(FakeFixtureSnapshot? snapshot = null)
    {
        snapshot ??= DeterministicFakeFixture.Create();
        var query = new ReportDataQueryService(
            new FakeOrganizationReadRepository(snapshot),
            new FakeProductReadRepository(snapshot),
            new FakeWorkOrderReadRepository(snapshot),
            new FakeProductionRecordReadRepository(snapshot),
            new FakeDailyProductionPlanReadRepository(snapshot),
            new FakeImportBatchReadRepository(snapshot));

        return new MonthlyProductionPlanReportService(
            query,
            new FakeDataAccessModeProvider(),
            new FixedClock());
    }

    private static MonthlyProductionPlanQueryRequest BaseRequest(
        long factoryId = DeterministicFakeFixture.FactoryDemo1Id,
        string planMonth = DeterministicFakeFixture.PlanYearMonth202603,
        long? workshopId = null,
        long? productionLineId = null,
        string? productCode = null) =>
        new()
        {
            FactoryId = factoryId,
            PlanMonth = planMonth,
            WorkshopId = workshopId,
            ProductionLineId = productionLineId,
            ProductCode = productCode
        };

    [Fact]
    public async Task Query_ByFactoryIdAndPlanMonth_ReturnsPublishedActiveRows()
    {
        var snapshot = DeterministicFakeFixture.Create();
        var service = CreateService(snapshot);
        var response = await service.QueryAsync(BaseRequest());

        Assert.Equal("monthly_production_plan", response.Meta.ReportCode);
        Assert.Equal(nameof(DataAccessMode.Fake), response.Meta.DataAccessMode);
        Assert.True(response.Meta.IsFake);
        Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, response.Meta.Filters.FactoryId);
        Assert.Equal(DeterministicFakeFixture.PlanYearMonth202603, response.Meta.Filters.PlanMonth);
        Assert.Equal(DeterministicFakeFixture.DatasetVersionFactory1Id, response.Meta.ActivePlanVersionId);
        Assert.Contains("FAKE DATA VERSION BEHAVIOR", response.Meta.FakeVersionRuleNote, StringComparison.Ordinal);
        Assert.Contains("Do not average", response.Meta.DailyPlanLinePrinciple, StringComparison.Ordinal);

        Assert.NotEmpty(response.Rows);
        Assert.All(response.Rows, r =>
        {
            Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, r.FactoryId);
            Assert.Equal(DeterministicFakeFixture.DatasetVersionFactory1Id, r.PlanVersionId);
            Assert.Equal(nameof(DatasetPublishStatus.Published), r.PublishStatus);
            Assert.True(r.IsActive);
            Assert.Equal(2026, r.PlanDate.Year);
            Assert.Equal(3, r.PlanDate.Month);
        });

        Assert.DoesNotContain(response.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductDraftOnly);
    }

    [Fact]
    public async Task Query_PlanDates_AllBelongToRequestedMonth()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(planMonth: "2026-03"));

        Assert.All(response.Rows, r =>
        {
            Assert.Equal(2026, r.PlanDate.Year);
            Assert.Equal(3, r.PlanDate.Month);
        });

        var emptyMonth = await service.QueryAsync(BaseRequest(planMonth: "2026-04"));
        Assert.Empty(emptyMonth.Rows);
    }

    [Fact]
    public async Task Query_WorkshopLineProductFilters_WorkIndependently()
    {
        var service = CreateService();

        var byWorkshop = await service.QueryAsync(BaseRequest(workshopId: DeterministicFakeFixture.WorkshopBId));
        Assert.NotEmpty(byWorkshop.Rows);
        Assert.All(byWorkshop.Rows, r => Assert.Equal(DeterministicFakeFixture.WorkshopBId, r.WorkshopId));

        var byLine = await service.QueryAsync(BaseRequest(productionLineId: DeterministicFakeFixture.LineA2Id));
        Assert.NotEmpty(byLine.Rows);
        Assert.All(byLine.Rows, r => Assert.Equal(DeterministicFakeFixture.LineA2Id, r.ProductionLineId));

        var byProduct = await service.QueryAsync(BaseRequest(productCode: DeterministicFakeFixture.ProductPlanOnly));
        var row = Assert.Single(byProduct.Rows);
        Assert.Equal(DeterministicFakeFixture.ProductPlanOnly, row.ProductCode);
        Assert.Equal(80m, row.PlanQuantity);
    }

    [Fact]
    public async Task Query_IsolatesFactories()
    {
        var service = CreateService();
        var f1 = await service.QueryAsync(BaseRequest(factoryId: DeterministicFakeFixture.FactoryDemo1Id));
        var f2 = await service.QueryAsync(BaseRequest(factoryId: DeterministicFakeFixture.FactoryDemo2Id));

        Assert.All(f1.Rows, r =>
        {
            Assert.Equal(1, r.FactoryId);
            Assert.NotEqual(DeterministicFakeFixture.ProductFactory2, r.ProductCode);
        });

        var f2Row = Assert.Single(f2.Rows);
        Assert.Equal(2, f2Row.FactoryId);
        Assert.Equal(DeterministicFakeFixture.ProductFactory2, f2Row.ProductCode);
        Assert.Equal(180m, f2Row.PlanQuantity);
    }

    [Fact]
    public async Task Query_ExcludesNonPublishedActiveVersions()
    {
        var snapshot = DeterministicFakeFixture.Create();
        Assert.Contains(snapshot.DatasetVersions, v =>
            v.Id == DeterministicFakeFixture.DatasetVersionFactory1DraftId
            && v.PublishStatus == DatasetPublishStatus.Draft
            && !v.IsActive);
        Assert.Contains(snapshot.DailyPlanLines, p =>
            p.PlanVersionId == DeterministicFakeFixture.DatasetVersionFactory1DraftId
            && p.ProductCode == DeterministicFakeFixture.ProductDraftOnly);

        var service = CreateService(snapshot);
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductDraftOnly));

        Assert.Empty(response.Rows);
    }

    [Fact]
    public async Task Query_RowsMatchFixtureOriginalLines_NoMonthlyAverageLogic()
    {
        var snapshot = DeterministicFakeFixture.Create();
        var service = CreateService(snapshot);
        var response = await service.QueryAsync(BaseRequest());

        var expected = snapshot.DailyPlanLines
            .Where(p => p.FactoryId == DeterministicFakeFixture.FactoryDemo1Id)
            .Where(p => p.PlanVersionId == DeterministicFakeFixture.DatasetVersionFactory1Id)
            .Where(p => p.PlanDate.Year == 2026 && p.PlanDate.Month == 3)
            .OrderBy(p => p.PlanDate)
            .ThenBy(p => p.WorkshopId)
            .ThenBy(p => p.ProductionLineId ?? 0)
            .ThenBy(p => p.ProductCode, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected.Count, response.Rows.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].PlanDate, response.Rows[i].PlanDate);
            Assert.Equal(expected[i].ProductCode, response.Rows[i].ProductCode);
            Assert.Equal(expected[i].PlanQuantity, response.Rows[i].PlanQuantity);
            Assert.Equal(expected[i].WorkshopId, response.Rows[i].WorkshopId);
            Assert.Equal(expected[i].ProductionLineId, response.Rows[i].ProductionLineId);
            Assert.Equal(expected[i].PlanVersionId, response.Rows[i].PlanVersionId);
        }

        // 明确：响应行与夹具原始日行一致，不存在按月均摊生成日计划的逻辑痕迹。
        Assert.DoesNotContain(response.Rows, r => r.PlanQuantity == 999m);
    }

    [Fact]
    public async Task Query_MissingFactoryId_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(new MonthlyProductionPlanQueryRequest
            {
                PlanMonth = DeterministicFakeFixture.PlanYearMonth202603
            }));
        Assert.True(ex.Errors.ContainsKey("FactoryId"));
    }

    [Fact]
    public async Task Query_InvalidPlanMonth_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(BaseRequest(planMonth: "2026/03")));
        Assert.True(ex.Errors.ContainsKey("PlanMonth"));

        var ex2 = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(BaseRequest(planMonth: "2026-3")));
        Assert.True(ex2.Errors.ContainsKey("PlanMonth"));

        var ex3 = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(new MonthlyProductionPlanQueryRequest
            {
                FactoryId = DeterministicFakeFixture.FactoryDemo1Id,
                PlanMonth = null
            }));
        Assert.True(ex3.Errors.ContainsKey("PlanMonth"));
    }

    [Fact]
    public void TryParseYearMonth_AcceptsStrictYyyyMm()
    {
        Assert.True(MonthlyProductionPlanReportService.TryParseYearMonth("2026-03", out var y, out var m));
        Assert.Equal(2026, y);
        Assert.Equal(3, m);
        Assert.False(MonthlyProductionPlanReportService.TryParseYearMonth("2026-13", out _, out _));
        Assert.False(MonthlyProductionPlanReportService.TryParseYearMonth("26-03", out _, out _));
    }
}
