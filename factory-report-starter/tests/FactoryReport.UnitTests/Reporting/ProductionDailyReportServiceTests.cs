using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Reporting.ProductionDaily;
using FactoryReport.Domain.Common;
using FactoryReport.Domain.Production;
using FactoryReport.Infrastructure.Fake;
using FactoryReport.UnitTests.Security;

namespace FactoryReport.UnitTests.Reporting;

public class ProductionDailyReportServiceTests
{
    private sealed class FixedClock : IUtcClock
    {
        public UtcInstant UtcNow { get; } =
            UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));
    }

    private static ProductionDailyReportService CreateService(FakeFixtureSnapshot? snapshot = null)
    {
        snapshot ??= DeterministicFakeFixture.Create();
        var query = new ReportDataQueryService(
            new FakeOrganizationReadRepository(snapshot),
            new FakeProductReadRepository(snapshot),
            new FakeWorkOrderReadRepository(snapshot),
            new FakeProductionRecordReadRepository(snapshot),
            new FakeDailyProductionPlanReadRepository(snapshot, new FactoryReport.Infrastructure.Import.FakeImportBatchWorkspace(snapshot)),
            new FakeImportBatchReadRepository(new FactoryReport.Infrastructure.Import.FakeImportBatchWorkspace(snapshot)));

        return new ProductionDailyReportService(
            query,
            new FakeDataAccessModeProvider(),
            new FixedClock(),
            new PermissiveReportQueryScopeService());
    }

    private static ProductionDailyQueryRequest BaseRequest(
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
            EndDate = end ?? DeterministicFakeFixture.Day2,
            WorkshopId = workshopId,
            ProductionLineId = productionLineId,
            ProductCode = productCode
        };

    [Fact]
    public async Task Query_Aggregates_CorrectQuantities_AndYield()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductNormal,
            workshopId: DeterministicFakeFixture.WorkshopAId,
            productionLineId: DeterministicFakeFixture.LineA1Id,
            end: DeterministicFakeFixture.Day1));

        var row = Assert.Single(response.Rows);
        Assert.Equal(DeterministicFakeFixture.Day1, row.ProductionDate);
        Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, row.FactoryId);
        Assert.Equal(DeterministicFakeFixture.FactoryDemo1Code, row.FactoryCode);
        Assert.Equal(DeterministicFakeFixture.WorkshopAId, row.WorkshopId);
        Assert.Equal("W-DEMO-A", row.WorkshopCode);
        Assert.Equal(DeterministicFakeFixture.LineA1Id, row.ProductionLineId);
        Assert.Equal("L-A1", row.ProductionLineCode);
        Assert.Equal(DeterministicFakeFixture.ProductNormal, row.ProductCode);
        Assert.Equal(100m, row.ActualQuantity);
        Assert.Equal(90m, row.GoodQuantity);
        Assert.Equal(5m, row.DefectQuantity);
        Assert.Equal(3m, row.ScrapQuantity);
        Assert.Equal(2m, row.ReworkQuantity);
        Assert.Equal(100m, row.InspectionQuantity);
        Assert.Equal(0.9m, row.YieldRate);
    }

    [Fact]
    public async Task Query_Isolates_ByFactoryId()
    {
        var service = CreateService();

        var factory1 = await service.QueryAsync(BaseRequest(
            factoryId: DeterministicFakeFixture.FactoryDemo1Id,
            end: DeterministicFakeFixture.Day1));
        var factory2 = await service.QueryAsync(BaseRequest(
            factoryId: DeterministicFakeFixture.FactoryDemo2Id,
            end: DeterministicFakeFixture.Day1));

        Assert.All(factory1.Rows, r => Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, r.FactoryId));
        Assert.DoesNotContain(factory1.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductFactory2);

        Assert.All(factory2.Rows, r => Assert.Equal(DeterministicFakeFixture.FactoryDemo2Id, r.FactoryId));
        Assert.Contains(factory2.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductFactory2);
        Assert.DoesNotContain(factory2.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductNormal);
    }

    [Fact]
    public async Task Query_Filters_ByDateRange()
    {
        var service = CreateService();

        var day1Only = await service.QueryAsync(BaseRequest(
            start: DeterministicFakeFixture.Day1,
            end: DeterministicFakeFixture.Day1));
        Assert.All(day1Only.Rows, r => Assert.Equal(DeterministicFakeFixture.Day1, r.ProductionDate));
        Assert.DoesNotContain(day1Only.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductDay2);

        var day2Only = await service.QueryAsync(BaseRequest(
            start: DeterministicFakeFixture.Day2,
            end: DeterministicFakeFixture.Day2));
        Assert.Contains(day2Only.Rows, r => r.ProductCode == DeterministicFakeFixture.ProductDay2);
        Assert.All(day2Only.Rows, r => Assert.Equal(DeterministicFakeFixture.Day2, r.ProductionDate));

        var emptyDay3 = await service.QueryAsync(BaseRequest(
            start: DeterministicFakeFixture.Day3,
            end: DeterministicFakeFixture.Day3));
        Assert.Empty(emptyDay3.Rows);
    }

    [Fact]
    public async Task Query_OptionalFilters_Workshop_Line_Product_EachApply()
    {
        var service = CreateService();

        var byWorkshop = await service.QueryAsync(BaseRequest(
            workshopId: DeterministicFakeFixture.WorkshopBId,
            end: DeterministicFakeFixture.Day1));
        Assert.NotEmpty(byWorkshop.Rows);
        Assert.All(byWorkshop.Rows, r => Assert.Equal(DeterministicFakeFixture.WorkshopBId, r.WorkshopId));

        var byLine = await service.QueryAsync(BaseRequest(
            productionLineId: DeterministicFakeFixture.LineA2Id,
            start: DeterministicFakeFixture.Day2,
            end: DeterministicFakeFixture.Day2));
        Assert.NotEmpty(byLine.Rows);
        Assert.All(byLine.Rows, r => Assert.Equal(DeterministicFakeFixture.LineA2Id, r.ProductionLineId));

        var byProduct = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductPlanZero,
            end: DeterministicFakeFixture.Day1));
        Assert.All(byProduct.Rows, r => Assert.Equal(DeterministicFakeFixture.ProductPlanZero, r.ProductCode));
        Assert.Single(byProduct.Rows);
    }

    [Fact]
    public void YieldRate_IsNull_WhenInspectionQuantityIsZero()
    {
        Assert.Null(ProductionDailyReportService.ComputeYieldRate(goodQuantity: 10m, inspectionQuantity: 0m));
        Assert.Equal(0.5m, ProductionDailyReportService.ComputeYieldRate(5m, 10m));
    }

    [Fact]
    public async Task Query_YieldRate_Null_WhenAggregatedInspectionIsZero()
    {
        var snapshot = DeterministicFakeFixture.Create();
        var zeroInsp = new ProductionRecord(
            factoryId: DeterministicFakeFixture.FactoryDemo1Id,
            workshopId: DeterministicFakeFixture.WorkshopAId,
            productionLineId: DeterministicFakeFixture.LineA1Id,
            productionDate: DeterministicFakeFixture.Day1,
            productCode: "PROD-ZERO-INSP",
            quantities: new ProductionQuantities(
                actualQuantity: 5m,
                goodQuantity: 0m,
                defectQuantity: 0m,
                scrapQuantity: 0m,
                reworkQuantity: 0m,
                inspectedQuantity: 0m),
            dataUpdatedAtUtc: DeterministicFakeFixture.FixedUpdatedAtUtc);

        var extended = new FakeFixtureSnapshot(
            snapshot.Factories,
            snapshot.Workshops,
            snapshot.ProductionLines,
            snapshot.Products,
            snapshot.WorkOrders,
            snapshot.ProductionRecords.Concat([zeroInsp]).ToList(),
            snapshot.DailyPlanLines,
            snapshot.ImportBatches,
            snapshot.DatasetVersions);

        var service = CreateService(extended);
        var response = await service.QueryAsync(BaseRequest(
            productCode: "PROD-ZERO-INSP",
            end: DeterministicFakeFixture.Day1));

        var row = Assert.Single(response.Rows);
        Assert.Equal(0m, row.InspectionQuantity);
        Assert.Null(row.YieldRate);
    }

    [Fact]
    public async Task Query_MissingFactoryId_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(new ProductionDailyQueryRequest
            {
                FactoryId = null,
                StartDate = DeterministicFakeFixture.Day1,
                EndDate = DeterministicFakeFixture.Day1
            }));

        Assert.Contains("FactoryId", ex.Errors.Keys);
    }

    [Fact]
    public async Task Query_InvalidDateRange_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(BaseRequest(
                start: DeterministicFakeFixture.Day2,
                end: DeterministicFakeFixture.Day1)));

        Assert.Contains("DateRange", ex.Errors.Keys);
    }

    [Fact]
    public async Task Query_Meta_ContainsReportCode_AndFakeMode()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(end: DeterministicFakeFixture.Day1));

        Assert.Equal("production_daily", response.Meta.ReportCode);
        Assert.Equal("Fake", response.Meta.DataAccessMode);
        Assert.True(response.Meta.IsFake);
        Assert.Contains("Fake 测试口径", response.Meta.YieldRateDisclaimer);
        Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, response.Meta.Filters.FactoryId);
    }

    [Fact]
    public async Task Query_UnknownFactory_ReturnsEmpty_NotCrossFactory()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(factoryId: 9999));
        Assert.Empty(response.Rows);
        Assert.Equal(9999, response.Meta.Filters.FactoryId);
    }
}
