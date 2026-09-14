using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Common;
using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Reporting.WorkOrderProgress;
using FactoryReport.Domain.Common;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.UnitTests.Reporting;

public class WorkOrderProgressReportServiceTests
{
    private sealed class FixedClock : IUtcClock
    {
        public FixedClock(UtcInstant utcNow) => UtcNow = utcNow;

        public UtcInstant UtcNow { get; }
    }

    private static WorkOrderProgressReportService CreateService(
        FakeFixtureSnapshot? snapshot = null,
        UtcInstant? comparedAt = null)
    {
        snapshot ??= DeterministicFakeFixture.Create();
        var query = new ReportDataQueryService(
            new FakeOrganizationReadRepository(snapshot),
            new FakeProductReadRepository(snapshot),
            new FakeWorkOrderReadRepository(snapshot),
            new FakeProductionRecordReadRepository(snapshot),
            new FakeDailyProductionPlanReadRepository(snapshot),
            new FakeImportBatchReadRepository(snapshot));

        return new WorkOrderProgressReportService(
            query,
            new FakeDataAccessModeProvider(),
            new FixedClock(comparedAt ?? DeterministicFakeFixture.FakeComparedAtUtc));
    }

    private static WorkOrderProgressQueryRequest BaseRequest(
        long factoryId = DeterministicFakeFixture.FactoryDemo1Id,
        long? workshopId = null,
        long? productionLineId = null,
        string? productCode = null,
        string? workOrderCode = null,
        string? status = null,
        DateOnly? plannedFinishFrom = null,
        DateOnly? plannedFinishTo = null) =>
        new()
        {
            FactoryId = factoryId,
            WorkshopId = workshopId,
            ProductionLineId = productionLineId,
            ProductCode = productCode,
            WorkOrderCode = workOrderCode,
            Status = status,
            PlannedFinishFrom = plannedFinishFrom,
            PlannedFinishTo = plannedFinishTo
        };

    [Fact]
    public async Task Query_Isolates_ByFactoryId()
    {
        var service = CreateService();

        var factory1 = await service.QueryAsync(BaseRequest(factoryId: DeterministicFakeFixture.FactoryDemo1Id));
        var factory2 = await service.QueryAsync(BaseRequest(factoryId: DeterministicFakeFixture.FactoryDemo2Id));

        Assert.All(factory1.Rows, r => Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, r.FactoryId));
        Assert.DoesNotContain(factory1.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderFactory2);

        Assert.All(factory2.Rows, r => Assert.Equal(DeterministicFakeFixture.FactoryDemo2Id, r.FactoryId));
        Assert.Contains(factory2.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderFactory2);
        Assert.DoesNotContain(factory2.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderNormal);
    }

    [Fact]
    public async Task Query_StatusFilter_Applies()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest(status: DeterministicFakeFixture.StatusClosed));

        Assert.NotEmpty(response.Rows);
        Assert.All(response.Rows, r => Assert.Equal(DeterministicFakeFixture.StatusClosed, r.Status));
        Assert.Contains(response.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderClosed);
    }

    [Fact]
    public async Task Query_WorkOrderCodeFilter_Applies()
    {
        var service = CreateService();
        var response = await service.QueryAsync(
            BaseRequest(workOrderCode: DeterministicFakeFixture.WorkOrderNormal));

        var row = Assert.Single(response.Rows);
        Assert.Equal(DeterministicFakeFixture.WorkOrderNormal, row.WorkOrderCode);
        Assert.Equal(DeterministicFakeFixture.ProductNormal, row.ProductCode);
        Assert.Equal(120m, row.PlannedQuantity);
        Assert.Equal(100m, row.ActualQuantity);
        Assert.Equal(100m / 120m, row.CompletionRate);
        Assert.Equal(DeterministicFakeFixture.FactoryDemo1Code, row.FactoryCode);
        Assert.Equal("W-DEMO-A", row.WorkshopCode);
        Assert.Equal("L-A1", row.ProductionLineCode);
        Assert.Equal(DeterministicFakeFixture.FixedPlannedStartUtc.Value, row.PlannedStartUtc);
        Assert.Equal(DeterministicFakeFixture.FixedPlannedFinishUtc.Value, row.PlannedFinishUtc);
        Assert.False(row.IsCompleted);
        Assert.False(row.IsOverdue);
    }

    [Fact]
    public async Task Query_ProductAndOrganizationFilters_Apply()
    {
        var service = CreateService();

        var byProduct = await service.QueryAsync(
            BaseRequest(productCode: DeterministicFakeFixture.ProductPlanZero));
        Assert.All(byProduct.Rows, r => Assert.Equal(DeterministicFakeFixture.ProductPlanZero, r.ProductCode));
        Assert.Contains(byProduct.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderPlanZero);

        var byWorkshop = await service.QueryAsync(
            BaseRequest(workshopId: DeterministicFakeFixture.WorkshopBId));
        Assert.NotEmpty(byWorkshop.Rows);
        Assert.All(byWorkshop.Rows, r => Assert.Equal(DeterministicFakeFixture.WorkshopBId, r.WorkshopId));

        var byLine = await service.QueryAsync(
            BaseRequest(productionLineId: DeterministicFakeFixture.LineA2Id));
        Assert.NotEmpty(byLine.Rows);
        Assert.All(byLine.Rows, r => Assert.Equal(DeterministicFakeFixture.LineA2Id, r.ProductionLineId));
    }

    [Fact]
    public async Task Query_PlannedFinishDateRange_Applies()
    {
        var service = CreateService();

        var earlyOnly = await service.QueryAsync(BaseRequest(
            plannedFinishFrom: DeterministicFakeFixture.Day2,
            plannedFinishTo: DeterministicFakeFixture.Day2));

        Assert.NotEmpty(earlyOnly.Rows);
        Assert.All(earlyOnly.Rows, r =>
        {
            Assert.NotNull(r.PlannedFinishUtc);
            Assert.Equal(
                DeterministicFakeFixture.Day2,
                DateOnly.FromDateTime(r.PlannedFinishUtc!.Value.UtcDateTime));
        });
        Assert.Contains(earlyOnly.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderOverdueOpen);
        Assert.DoesNotContain(earlyOnly.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderNormal);

        var day3 = await service.QueryAsync(BaseRequest(
            plannedFinishFrom: DeterministicFakeFixture.Day3,
            plannedFinishTo: DeterministicFakeFixture.Day3));
        Assert.Contains(day3.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderNormal);
        Assert.DoesNotContain(day3.Rows, r => r.WorkOrderCode == DeterministicFakeFixture.WorkOrderOverdueOpen);
    }

    [Fact]
    public async Task Query_CompletionRate_Null_WhenPlannedQuantityIsZero()
    {
        var service = CreateService();
        var response = await service.QueryAsync(
            BaseRequest(workOrderCode: DeterministicFakeFixture.WorkOrderPlanZero));

        var row = Assert.Single(response.Rows);
        Assert.Equal(0m, row.PlannedQuantity);
        Assert.Null(row.CompletionRate);
        Assert.Equal(0m, row.RemainingQuantity);
    }

    [Fact]
    public void CompletionRate_AndRemaining_BoundaryRules()
    {
        Assert.Null(WorkOrderProgressReportService.ComputeCompletionRate(0m, 40m));
        Assert.Equal(0.5m, WorkOrderProgressReportService.ComputeCompletionRate(100m, 50m));
        Assert.Equal(2m, WorkOrderProgressReportService.ComputeCompletionRate(100m, 200m));
        Assert.Equal(0m, WorkOrderProgressReportService.ComputeRemaining(100m, 200m));
        Assert.Equal(20m, WorkOrderProgressReportService.ComputeRemaining(100m, 80m));
    }

    [Fact]
    public async Task Query_ClosedAndCompleted_AreNotOverdue_EvenPastPlannedFinish()
    {
        var service = CreateService(comparedAt: DeterministicFakeFixture.FakeComparedAtUtc);

        var closed = await service.QueryAsync(
            BaseRequest(workOrderCode: DeterministicFakeFixture.WorkOrderClosed));
        var closedRow = Assert.Single(closed.Rows);
        Assert.True(closedRow.IsCompleted);
        Assert.False(closedRow.IsOverdue);

        var done = await service.QueryAsync(
            BaseRequest(workOrderCode: DeterministicFakeFixture.WorkOrderCompleted));
        var doneRow = Assert.Single(done.Rows);
        Assert.True(doneRow.IsCompleted);
        Assert.False(doneRow.IsOverdue);
    }

    [Fact]
    public async Task Query_OpenPastPlannedFinish_IsOverdue()
    {
        var service = CreateService(comparedAt: DeterministicFakeFixture.FakeComparedAtUtc);
        var response = await service.QueryAsync(
            BaseRequest(workOrderCode: DeterministicFakeFixture.WorkOrderOverdueOpen));

        var row = Assert.Single(response.Rows);
        Assert.False(row.IsCompleted);
        Assert.True(row.IsOverdue);
        Assert.Equal(DeterministicFakeFixture.StatusOpen, row.Status);
    }

    [Fact]
    public async Task Query_TimeProvider_ControlsOverdue()
    {
        var beforeFinish = UtcInstant.FromUtcDateTime(
            new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc));
        var afterFinish = DeterministicFakeFixture.FakeComparedAtUtc;

        var notYet = await CreateService(comparedAt: beforeFinish)
            .QueryAsync(BaseRequest(workOrderCode: DeterministicFakeFixture.WorkOrderOverdueOpen));
        Assert.False(Assert.Single(notYet.Rows).IsOverdue);
        Assert.Equal(beforeFinish.Value, notYet.Meta.ComparedAtUtc);

        var overdue = await CreateService(comparedAt: afterFinish)
            .QueryAsync(BaseRequest(workOrderCode: DeterministicFakeFixture.WorkOrderOverdueOpen));
        Assert.True(Assert.Single(overdue.Rows).IsOverdue);
        Assert.Equal(afterFinish.Value, overdue.Meta.ComparedAtUtc);
    }

    [Fact]
    public async Task Query_MissingFactoryId_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(new WorkOrderProgressQueryRequest { FactoryId = null }));

        Assert.Contains("FactoryId", ex.Errors.Keys);
    }

    [Fact]
    public async Task Query_InvalidPlannedFinishRange_ThrowsValidation()
    {
        var service = CreateService();
        var ex = await Assert.ThrowsAsync<ReportQueryValidationException>(() =>
            service.QueryAsync(BaseRequest(
                plannedFinishFrom: DeterministicFakeFixture.Day3,
                plannedFinishTo: DeterministicFakeFixture.Day1)));

        Assert.Contains("PlannedFinishDateRange", ex.Errors.Keys);
    }

    [Fact]
    public async Task Query_Meta_ContainsReportCode_AndFakeMode()
    {
        var service = CreateService();
        var response = await service.QueryAsync(BaseRequest());

        Assert.Equal("work_order_progress", response.Meta.ReportCode);
        Assert.Equal("Fake", response.Meta.DataAccessMode);
        Assert.True(response.Meta.IsFake);
        Assert.Contains("Fake 测试规则", response.Meta.OverdueDisclaimer);
        Assert.Contains("IsOverdue", response.Meta.OverdueRule);
        Assert.Equal(DeterministicFakeFixture.FakeComparedAtUtc.Value, response.Meta.ComparedAtUtc);
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

    [Fact]
    public void EvaluateOverdue_Rules()
    {
        var finish = DeterministicFakeFixture.EarlyPlannedFinishUtc;
        var after = DeterministicFakeFixture.FakeComparedAtUtc;
        var before = UtcInstant.FromUtcDateTime(new DateTime(2026, 3, 11, 10, 0, 0, DateTimeKind.Utc));

        Assert.False(WorkOrderProgressReportService.EvaluateOverdue(isCompleted: true, finish, after));
        Assert.False(WorkOrderProgressReportService.EvaluateOverdue(isCompleted: false, null, after));
        Assert.False(WorkOrderProgressReportService.EvaluateOverdue(isCompleted: false, finish, before));
        Assert.True(WorkOrderProgressReportService.EvaluateOverdue(isCompleted: false, finish, after));
    }
}
