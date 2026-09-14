using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Reporting;
using FactoryReport.Application.Reporting.ProductionPlanAchievement;
using FactoryReport.Domain.Common;
using FactoryReport.Domain.Reporting;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.UnitTests.Reporting;

public class ProductionPlanAchievementReportServiceTests
{
    private sealed class FixedClock : IUtcClock
    {
        public UtcInstant UtcNow { get; } =
            UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));
    }

    private static ProductionPlanAchievementReportService CreateService(FakeFixtureSnapshot? snapshot = null)
    {
        snapshot ??= DeterministicFakeFixture.Create();
        var query = new ReportDataQueryService(
            new FakeOrganizationReadRepository(snapshot),
            new FakeProductReadRepository(snapshot),
            new FakeWorkOrderReadRepository(snapshot),
            new FakeProductionRecordReadRepository(snapshot),
            new FakeDailyProductionPlanReadRepository(snapshot),
            new FakeImportBatchReadRepository(snapshot));

        return new ProductionPlanAchievementReportService(
            query,
            new FakeDataAccessModeProvider(),
            new FixedClock());
    }

    private static ProductionPlanAchievementQueryRequest BaseRequest(
        long factoryId = DeterministicFakeFixture.FactoryDemo1Id,
        DateOnly? start = null,
        DateOnly? end = null,
        long? workshopId = null,
        long? productionLineId = null,
        string? productCode = null) =>
        new()
        {
            FactoryId = factoryId,
            StartDate = start ?? DeterministicFakeFixture.Day1,
            EndDate = end ?? DeterministicFakeFixture.Day1,
            WorkshopId = workshopId,
            ProductionLineId = productionLineId,
            ProductCode = productCode
        };

    [Fact]
    public async Task Query_NormalPlanAndActual_Calculated()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductNormal,
            workshopId: DeterministicFakeFixture.WorkshopAId,
            productionLineId: DeterministicFakeFixture.LineA1Id));

        var row = Assert.Single(response.Rows);
        Assert.Equal(120m, row.PlanQuantity);
        Assert.Equal(100m, row.ActualQuantity);
        Assert.Equal(100m / 120m, row.AchievementRate);
        Assert.Equal(nameof(PlanAchievementStatus.Calculated), row.PlanStatus);
        Assert.Equal(DeterministicFakeFixture.FactoryDemo1Code, row.FactoryCode);
        Assert.Equal("W-DEMO-A", row.WorkshopCode);
        Assert.Equal("L-A1", row.ProductionLineCode);
    }

    [Fact]
    public async Task Query_PlanQuantityZero_AchievementRateNull()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductPlanZero));

        var row = Assert.Single(response.Rows);
        Assert.Equal(0m, row.PlanQuantity);
        Assert.Equal(40m, row.ActualQuantity);
        Assert.Null(row.AchievementRate);
        Assert.Equal(nameof(PlanAchievementStatus.PlanIsZero), row.PlanStatus);
    }

    [Fact]
    public async Task Query_ActualWithoutPlan_PlanNotConfigured_NotZeroPercent()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductActualOnly));

        var row = Assert.Single(response.Rows);
        Assert.Null(row.PlanQuantity);
        Assert.Equal(55m, row.ActualQuantity);
        Assert.Null(row.AchievementRate);
        Assert.Equal(nameof(PlanAchievementStatus.PlanNotConfigured), row.PlanStatus);
        Assert.NotEqual(0m, row.AchievementRate);
    }

    [Fact]
    public async Task Query_PlanWithoutActual_MissingActual_ZeroRate()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductPlanOnly));

        var row = Assert.Single(response.Rows);
        Assert.Equal(80m, row.PlanQuantity);
        Assert.Equal(0m, row.ActualQuantity);
        Assert.Equal(0m, row.AchievementRate);
        Assert.Equal(nameof(PlanAchievementStatus.MissingActual), row.PlanStatus);
    }

    [Fact]
    public async Task Query_IsolatesFactories()
    {
        var service = CreateService();
        var f1 = await service.QueryAsync(BaseRequest(factoryId: 1));
        var f2 = await service.QueryAsync(BaseRequest(factoryId: 2));

        Assert.All(f1.Rows, r => Assert.Equal(1, r.FactoryId));
        Assert.DoesNotContain(f1.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductFactory2);

        var f2Row = Assert.Single(f2.Rows);
        Assert.Equal(DeterministicFakeFixture.ProductFactory2, f2Row.ProductCode);
        Assert.Equal(180m, f2Row.PlanQuantity);
        Assert.Equal(200m, f2Row.ActualQuantity);
        Assert.Equal(nameof(PlanAchievementStatus.Calculated), f2Row.PlanStatus);
    }

    [Fact]
    public async Task Query_UnknownFactory_ReturnsEmptyRows()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(factoryId: 999));
        Assert.Empty(response.Rows);
        Assert.Equal(StableReportCodes.ProductionPlanAchievement, response.Meta.ReportCode);
    }

    [Fact]
    public async Task Query_DateWorkshopLineProductFilters_Apply()
    {
        var service = CreateService();

        var day2 = await service.QueryAsync(BaseRequest(
            start: DeterministicFakeFixture.Day2,
            end: DeterministicFakeFixture.Day2));
        Assert.All(day2.Rows, r => Assert.Equal(DeterministicFakeFixture.Day2, r.ProductionDate));
        Assert.Contains(day2.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductDay2);

        var workshopB = await service.QueryAsync(BaseRequest(
            workshopId: DeterministicFakeFixture.WorkshopBId));
        Assert.All(workshopB.Rows, r => Assert.Equal(DeterministicFakeFixture.WorkshopBId, r.WorkshopId));

        var lineA2 = await service.QueryAsync(BaseRequest(
            start: DeterministicFakeFixture.Day2,
            end: DeterministicFakeFixture.Day2,
            productionLineId: DeterministicFakeFixture.LineA2Id));
        Assert.All(lineA2.Rows, r => Assert.Equal(DeterministicFakeFixture.LineA2Id, r.ProductionLineId));

        var byProduct = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductPlanZero));
        Assert.Equal(
            DeterministicFakeFixture.ProductPlanZero,
            Assert.Single(byProduct.Rows).ProductCode);
    }

    [Fact]
    public async Task Query_InvalidDateRange_ThrowsProblemDetailsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(BaseRequest(
                start: DeterministicFakeFixture.Day2,
                end: DeterministicFakeFixture.Day1)));
        Assert.True(ex.Errors.ContainsKey("DateRange"));
    }

    [Fact]
    public async Task Query_MissingFactoryId_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(new ProductionPlanAchievementQueryRequest
            {
                FactoryId = null,
                StartDate = DeterministicFakeFixture.Day1,
                EndDate = DeterministicFakeFixture.Day1
            }));
        Assert.True(ex.Errors.ContainsKey("FactoryId"));
    }

    [Fact]
    public async Task Query_Meta_IncludesReportCode_AndFakeMode()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest());
        Assert.Equal(StableReportCodes.ProductionPlanAchievement, response.Meta.ReportCode);
        Assert.Equal("Fake", response.Meta.DataAccessMode);
        Assert.True(response.Meta.IsFake);
        Assert.Contains("FactoryId + WorkshopId + ProductionLineId", response.Meta.MatchKey);
        Assert.Contains("PlanNotConfigured", response.Meta.AchievementRules);
    }

    [Fact]
    public async Task Query_EmptyDateRange_ReturnsEmpty_NotOtherFactoryData()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            start: DeterministicFakeFixture.Day3,
            end: DeterministicFakeFixture.Day3));
        Assert.Empty(response.Rows);
    }
}
