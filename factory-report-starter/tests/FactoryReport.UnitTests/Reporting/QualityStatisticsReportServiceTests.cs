using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Reporting.QualityStatistics;
using FactoryReport.Domain.Common;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.UnitTests.Reporting;

public class QualityStatisticsReportServiceTests
{
    private sealed class FixedClock : IUtcClock
    {
        public UtcInstant UtcNow { get; } =
            UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));
    }

    private static QualityStatisticsReportService CreateService(FakeFixtureSnapshot? snapshot = null)
    {
        snapshot ??= DeterministicFakeFixture.Create();
        var query = new ReportDataQueryService(
            new FakeOrganizationReadRepository(snapshot),
            new FakeProductReadRepository(snapshot),
            new FakeWorkOrderReadRepository(snapshot),
            new FakeProductionRecordReadRepository(snapshot),
            new FakeDailyProductionPlanReadRepository(snapshot),
            new FakeImportBatchReadRepository(snapshot));

        return new QualityStatisticsReportService(
            query,
            new FakeDataAccessModeProvider(),
            new FixedClock());
    }

    private static QualityStatisticsQueryRequest BaseRequest(
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
    public async Task Query_Aggregates_CorrectQuantities_AndRates()
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
        Assert.Equal(100m, row.InspectionQuantity);
        Assert.Equal(90m, row.GoodQuantity);
        Assert.Equal(5m, row.DefectQuantity);
        Assert.Equal(3m, row.ScrapQuantity);
        Assert.Equal(2m, row.ReworkQuantity);
        Assert.Equal(0.9m, row.YieldRate);
        Assert.Equal(0.05m, row.DefectRate);
    }

    [Fact]
    public async Task Query_DoesNotMerge_ScrapOrRework_IntoDefect()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductNormal,
            workshopId: DeterministicFakeFixture.WorkshopAId,
            productionLineId: DeterministicFakeFixture.LineA1Id,
            end: DeterministicFakeFixture.Day1));

        var row = Assert.Single(response.Rows);
        // Defect 必须保持 5，不得被错误写成 5+3+2=10
        Assert.Equal(5m, row.DefectQuantity);
        Assert.Equal(3m, row.ScrapQuantity);
        Assert.Equal(2m, row.ReworkQuantity);
        Assert.NotEqual(row.DefectQuantity + row.ScrapQuantity + row.ReworkQuantity, row.DefectQuantity);
        // DefectRate 分子仅为 DefectQuantity
        Assert.Equal(0.05m, row.DefectRate);
        Assert.NotEqual((5m + 3m + 2m) / 100m, row.DefectRate);
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
    public void Rates_AreNull_WhenInspectionQuantityIsZero()
    {
        Assert.Null(QualityStatisticsReportService.ComputeYieldRate(10m, 0m));
        Assert.Null(QualityStatisticsReportService.ComputeDefectRate(10m, 0m));
        Assert.Equal(0.5m, QualityStatisticsReportService.ComputeYieldRate(5m, 10m));
        Assert.Equal(0.2m, QualityStatisticsReportService.ComputeDefectRate(2m, 10m));
    }

    [Fact]
    public async Task Query_Rates_Null_WhenAggregatedInspectionIsZero()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(
            productCode: DeterministicFakeFixture.ProductZeroInspection,
            end: DeterministicFakeFixture.Day1));

        var row = Assert.Single(response.Rows);
        Assert.Equal(0m, row.InspectionQuantity);
        Assert.Null(row.YieldRate);
        Assert.Null(row.DefectRate);
        // Scrap/Rework 仍分列存在，且未并入 Defect
        Assert.Equal(0m, row.DefectQuantity);
        Assert.Equal(1m, row.ScrapQuantity);
        Assert.Equal(1m, row.ReworkQuantity);
    }

    [Fact]
    public async Task Query_MissingFactoryId_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(new QualityStatisticsQueryRequest
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

        Assert.Equal("quality_statistics", response.Meta.ReportCode);
        Assert.Equal("Fake", response.Meta.DataAccessMode);
        Assert.True(response.Meta.IsFake);
        Assert.Contains("Fake 测试口径", response.Meta.QualityMetricsDisclaimer);
        Assert.Contains("workOrderCode", response.Meta.WorkOrderFilterNote);
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
