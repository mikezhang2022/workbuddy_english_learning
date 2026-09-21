using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Reporting;
using FactoryReport.Domain.Production;
using FactoryReport.Domain.Reporting;
using FactoryReport.Infrastructure.Fake;
using FactoryReport.Infrastructure.Persistence.Oracle;

namespace FactoryReport.UnitTests.Fake;

public class DeterministicFakeFixtureTests
{
    [Fact]
    public void Create_IsDeterministic_AcrossInvocations()
    {
        var a = DeterministicFakeFixture.Create();
        var b = DeterministicFakeFixture.Create();

        Assert.Equal(a.Factories.Select(f => f.Id), b.Factories.Select(f => f.Id));
        Assert.Equal(a.Workshops.Select(w => (w.Id, w.FactoryId)), b.Workshops.Select(w => (w.Id, w.FactoryId)));
        Assert.Equal(a.ProductionLines.Select(l => (l.Id, l.FactoryId)), b.ProductionLines.Select(l => (l.Id, l.FactoryId)));
        Assert.Equal(a.Products.Select(p => (p.Id, p.ProductCode)), b.Products.Select(p => (p.Id, p.ProductCode)));
        Assert.Equal(a.WorkOrders.Select(w => (w.Id, w.WorkOrderNo)), b.WorkOrders.Select(w => (w.Id, w.WorkOrderNo)));
        Assert.Equal(
            a.ProductionRecords.Select(r => (r.FactoryId, r.ProductCode, r.ProductionDate, r.Quantities.ActualQuantity)),
            b.ProductionRecords.Select(r => (r.FactoryId, r.ProductCode, r.ProductionDate, r.Quantities.ActualQuantity)));
        Assert.Equal(
            a.DailyPlanLines.Select(p => (p.FactoryId, p.ProductCode, p.PlanDate, p.PlanQuantity)),
            b.DailyPlanLines.Select(p => (p.FactoryId, p.ProductCode, p.PlanDate, p.PlanQuantity)));
        Assert.Equal(a.ImportBatches.Select(i => i.Id), b.ImportBatches.Select(i => i.Id));
        Assert.Equal(a.DatasetVersions.Select(v => v.Id), b.DatasetVersions.Select(v => v.Id));
        Assert.Equal(DeterministicFakeFixture.FixedUpdatedAtUtc, a.ProductionRecords[0].DataUpdatedAtUtc);
        Assert.Equal(a.ProductionRecords[0].DataUpdatedAtUtc, b.ProductionRecords[0].DataUpdatedAtUtc);
    }

    [Fact]
    public void Fixture_UsesFixedUtcDates_NotSystemClock()
    {
        var snapshot = DeterministicFakeFixture.Create();

        Assert.All(snapshot.ProductionRecords, r =>
        {
            Assert.True(r.ProductionDate >= DeterministicFakeFixture.MinDate);
            Assert.True(r.ProductionDate <= DeterministicFakeFixture.MaxDate);
            Assert.Equal(DeterministicFakeFixture.FixedUpdatedAtUtc, r.DataUpdatedAtUtc);
        });

        Assert.All(snapshot.DailyPlanLines, p =>
        {
            Assert.True(p.PlanDate >= DeterministicFakeFixture.MinDate);
            Assert.True(p.PlanDate <= DeterministicFakeFixture.MaxDate);
        });
    }

    [Fact]
    public void Fixture_CoversTwoFactories_MultipleWorkshopsAndLines()
    {
        var snapshot = DeterministicFakeFixture.Create();

        Assert.Equal(2, snapshot.Factories.Count);
        Assert.True(snapshot.Workshops.Count(w => w.FactoryId == DeterministicFakeFixture.FactoryDemo1Id) >= 2);
        Assert.True(snapshot.ProductionLines.Count(l => l.FactoryId == DeterministicFakeFixture.FactoryDemo1Id) >= 3);
        Assert.Contains(snapshot.Workshops, w => w.FactoryId == DeterministicFakeFixture.FactoryDemo2Id);
        Assert.Contains(snapshot.ProductionLines, l => l.FactoryId == DeterministicFakeFixture.FactoryDemo2Id);
    }
}

public class FakeRepositoryIsolationAndFilterTests
{
    private static ReportDataQueryService CreateQueryService(FakeFixtureSnapshot? snapshot = null)
    {
        snapshot ??= DeterministicFakeFixture.Create();
        return new ReportDataQueryService(
            new FakeOrganizationReadRepository(snapshot),
            new FakeProductReadRepository(snapshot),
            new FakeWorkOrderReadRepository(snapshot),
            new FakeProductionRecordReadRepository(snapshot),
            new FakeDailyProductionPlanReadRepository(snapshot, new FactoryReport.Infrastructure.Import.FakeImportBatchWorkspace(snapshot)),
            new FakeImportBatchReadRepository(new FactoryReport.Infrastructure.Import.FakeImportBatchWorkspace(snapshot)));
    }

    [Fact]
    public async Task FakeRepositories_AreInProcessOnly_NoOraclePlaceholderActivation()
    {
        var mode = new FakeDataAccessModeProvider();
        Assert.True(mode.IsFake);
        Assert.Equal(Application.Abstractions.DataAccessMode.Fake, mode.Mode);

        // Oracle 占位仅文档导航，不得被 Fake 路径激活连接。
        Assert.Equal("NotConfigured_PendingOnSiteConfirmation", OraclePersistencePlaceholder.Status);
        Assert.NotEmpty(OraclePersistencePlaceholder.ReplacementRepositoryInterfaces);

        var store = new FakePlaceholderDataStore();
        Assert.Contains("no database", store.Describe(), StringComparison.OrdinalIgnoreCase);

        var query = CreateQueryService();
        var factories = await query.GetFactoriesAsync();
        Assert.NotEmpty(factories);
    }

    [Fact]
    public async Task FactoryIsolation_DoesNotLeakAcrossFactories()
    {
        var query = CreateQueryService();
        var day1 = new DateRangeFilter(DeterministicFakeFixture.Day1, DeterministicFakeFixture.Day1);

        var f1Records = await query.GetProductionRecordsAsync(
            new OrganizationScopeFilter(DeterministicFakeFixture.FactoryDemo1Id),
            day1);
        var f2Records = await query.GetProductionRecordsAsync(
            new OrganizationScopeFilter(DeterministicFakeFixture.FactoryDemo2Id),
            day1);

        Assert.All(f1Records, r => Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, r.FactoryId));
        Assert.All(f2Records, r => Assert.Equal(DeterministicFakeFixture.FactoryDemo2Id, r.FactoryId));
        Assert.DoesNotContain(f1Records, r => r.ProductCode == DeterministicFakeFixture.ProductFactory2);
        Assert.Contains(f2Records, r => r.ProductCode == DeterministicFakeFixture.ProductFactory2);

        var f1Plans = await query.GetDailyPlanLinesAsync(
            new OrganizationScopeFilter(DeterministicFakeFixture.FactoryDemo1Id),
            day1);
        Assert.DoesNotContain(f1Plans, p => p.ProductCode == DeterministicFakeFixture.ProductFactory2);

        var f1WorkOrders = await query.GetWorkOrdersAsync(
            new OrganizationScopeFilter(DeterministicFakeFixture.FactoryDemo1Id));
        Assert.All(f1WorkOrders, w => Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, w.FactoryId));

        var f1Batches = await query.GetImportBatchesAsync(DeterministicFakeFixture.FactoryDemo1Id);
        Assert.All(f1Batches, b => Assert.Equal(DeterministicFakeFixture.FactoryDemo1Id, b.FactoryId));
    }

    [Fact]
    public async Task OrganizationScope_FiltersWorkshopAndLine()
    {
        var query = CreateQueryService();
        var day1 = new DateRangeFilter(DeterministicFakeFixture.Day1, DeterministicFakeFixture.Day1);

        var workshopA = await query.GetProductionRecordsAsync(
            new OrganizationScopeFilter(
                DeterministicFakeFixture.FactoryDemo1Id,
                workshopId: DeterministicFakeFixture.WorkshopAId),
            day1);

        Assert.All(workshopA, r => Assert.Equal(DeterministicFakeFixture.WorkshopAId, r.WorkshopId));
        Assert.DoesNotContain(workshopA, r => r.WorkshopId == DeterministicFakeFixture.WorkshopBId);

        var lineA1 = await query.GetProductionRecordsAsync(
            new OrganizationScopeFilter(
                DeterministicFakeFixture.FactoryDemo1Id,
                workshopId: DeterministicFakeFixture.WorkshopAId,
                productionLineId: DeterministicFakeFixture.LineA1Id),
            day1);

        Assert.All(lineA1, r => Assert.Equal(DeterministicFakeFixture.LineA1Id, r.ProductionLineId));
    }

    [Fact]
    public async Task DateRangeFilter_ExcludesOutOfRangeRows()
    {
        var query = CreateQueryService();
        var scope = new OrganizationScopeFilter(DeterministicFakeFixture.FactoryDemo1Id);

        var day1Only = await query.GetProductionRecordsAsync(
            scope,
            new DateRangeFilter(DeterministicFakeFixture.Day1, DeterministicFakeFixture.Day1));
        Assert.All(day1Only, r => Assert.Equal(DeterministicFakeFixture.Day1, r.ProductionDate));
        Assert.DoesNotContain(day1Only, r => r.ProductCode == DeterministicFakeFixture.ProductDay2);

        var day2Only = await query.GetProductionRecordsAsync(
            scope,
            new DateRangeFilter(DeterministicFakeFixture.Day2, DeterministicFakeFixture.Day2));
        Assert.Contains(day2Only, r => r.ProductCode == DeterministicFakeFixture.ProductDay2);
        Assert.DoesNotContain(day2Only, r => r.ProductionDate == DeterministicFakeFixture.Day1);

        var day3Empty = await query.GetProductionRecordsAsync(
            scope,
            new DateRangeFilter(DeterministicFakeFixture.Day3, DeterministicFakeFixture.Day3));
        Assert.Empty(day3Empty);

        var plansDay2 = await query.GetDailyPlanLinesAsync(
            scope,
            new DateRangeFilter(DeterministicFakeFixture.Day2, DeterministicFakeFixture.Day2));
        Assert.All(plansDay2, p => Assert.Equal(DeterministicFakeFixture.Day2, p.PlanDate));
    }
}

public class FakePlanActualBoundaryScenarioTests
{
    private static ReportDataQueryService CreateQueryService()
    {
        var snapshot = DeterministicFakeFixture.Create();
        return new ReportDataQueryService(
            new FakeOrganizationReadRepository(snapshot),
            new FakeProductReadRepository(snapshot),
            new FakeWorkOrderReadRepository(snapshot),
            new FakeProductionRecordReadRepository(snapshot),
            new FakeDailyProductionPlanReadRepository(snapshot, new FactoryReport.Infrastructure.Import.FakeImportBatchWorkspace(snapshot)),
            new FakeImportBatchReadRepository(new FactoryReport.Infrastructure.Import.FakeImportBatchWorkspace(snapshot)));
    }

    private static async Task<(decimal? Plan, decimal? Actual, ProductionQuantitiesSnapshot? Qty)> LoadPlanActualAsync(
        ReportDataQueryService query,
        string productCode)
    {
        var scope = new OrganizationScopeFilter(
            DeterministicFakeFixture.FactoryDemo1Id,
            workshopId: DeterministicFakeFixture.WorkshopAId,
            productionLineId: DeterministicFakeFixture.LineA1Id);
        var day1 = new DateRangeFilter(DeterministicFakeFixture.Day1, DeterministicFakeFixture.Day1);

        var plans = await query.GetDailyPlanLinesAsync(scope, day1, productCode);
        var records = await query.GetProductionRecordsAsync(scope, day1, productCode);

        decimal? plan = plans.Count == 0 ? null : plans.Single().PlanQuantity;
        decimal? actual = records.Count == 0 ? null : records.Single().Quantities.ActualQuantity;
        ProductionQuantitiesSnapshot? qty = records.Count == 0
            ? null
            : new ProductionQuantitiesSnapshot(records.Single().Quantities);

        return (plan, actual, qty);
    }

    private sealed record ProductionQuantitiesSnapshot(
        decimal Actual,
        decimal Good,
        decimal Defect,
        decimal Scrap,
        decimal Rework,
        decimal Inspected)
    {
        public ProductionQuantitiesSnapshot(ProductionQuantities q)
            : this(q.ActualQuantity, q.GoodQuantity, q.DefectQuantity, q.ScrapQuantity, q.ReworkQuantity, q.InspectedQuantity)
        {
        }
    }

    [Fact]
    public async Task ScenarioA_NormalPlanAndActual_Calculated()
    {
        var query = CreateQueryService();
        var (plan, actual, qty) = await LoadPlanActualAsync(query, DeterministicFakeFixture.ProductNormal);

        Assert.Equal(120m, plan);
        Assert.Equal(100m, actual);
        Assert.NotNull(qty);

        var key = new PlanAchievementKey(
            DeterministicFakeFixture.FactoryDemo1Id,
            DeterministicFakeFixture.WorkshopAId,
            DeterministicFakeFixture.LineA1Id,
            DeterministicFakeFixture.Day1,
            DeterministicFakeFixture.ProductNormal);
        var result = new PlanAchievementEvaluator().Evaluate(key, plan, actual);

        Assert.Equal(PlanAchievementStatus.Calculated, result.Status);
        Assert.Equal(100m / 120m, result.AchievementRate);
    }

    [Fact]
    public async Task ScenarioB_PlanIsZero_AchievementRateNull()
    {
        var query = CreateQueryService();
        var (plan, actual, _) = await LoadPlanActualAsync(query, DeterministicFakeFixture.ProductPlanZero);

        Assert.Equal(0m, plan);
        Assert.Equal(40m, actual);

        var key = new PlanAchievementKey(
            DeterministicFakeFixture.FactoryDemo1Id,
            DeterministicFakeFixture.WorkshopAId,
            DeterministicFakeFixture.LineA1Id,
            DeterministicFakeFixture.Day1,
            DeterministicFakeFixture.ProductPlanZero);
        var result = new PlanAchievementEvaluator().Evaluate(key, plan, actual);

        Assert.Equal(PlanAchievementStatus.PlanIsZero, result.Status);
        Assert.Null(result.AchievementRate);
    }

    [Fact]
    public async Task ScenarioC_ActualWithoutPlan_PlanNotConfigured()
    {
        var query = CreateQueryService();
        var (plan, actual, _) = await LoadPlanActualAsync(query, DeterministicFakeFixture.ProductActualOnly);

        Assert.Null(plan);
        Assert.Equal(55m, actual);

        var key = new PlanAchievementKey(
            DeterministicFakeFixture.FactoryDemo1Id,
            DeterministicFakeFixture.WorkshopAId,
            DeterministicFakeFixture.LineA1Id,
            DeterministicFakeFixture.Day1,
            DeterministicFakeFixture.ProductActualOnly);
        var result = new PlanAchievementEvaluator().Evaluate(key, plan, actual);

        Assert.Equal(PlanAchievementStatus.PlanNotConfigured, result.Status);
        Assert.Null(result.AchievementRate);
    }

    [Fact]
    public async Task ScenarioD_PlanWithoutActual_MissingActualZeroPercent()
    {
        var query = CreateQueryService();
        var (plan, actual, _) = await LoadPlanActualAsync(query, DeterministicFakeFixture.ProductPlanOnly);

        Assert.Equal(80m, plan);
        Assert.Null(actual);

        var key = new PlanAchievementKey(
            DeterministicFakeFixture.FactoryDemo1Id,
            DeterministicFakeFixture.WorkshopAId,
            DeterministicFakeFixture.LineA1Id,
            DeterministicFakeFixture.Day1,
            DeterministicFakeFixture.ProductPlanOnly);
        var result = new PlanAchievementEvaluator().Evaluate(key, plan, actual);

        Assert.Equal(PlanAchievementStatus.MissingActual, result.Status);
        Assert.Equal(0m, result.ActualQuantity);
        Assert.Equal(0m, result.AchievementRate);
    }

    [Fact]
    public async Task ScenarioE_FullQuantityColumns_Readable()
    {
        var query = CreateQueryService();
        var (_, _, qty) = await LoadPlanActualAsync(query, DeterministicFakeFixture.ProductNormal);

        Assert.NotNull(qty);
        Assert.Equal(100m, qty!.Actual);
        Assert.Equal(90m, qty.Good);
        Assert.Equal(5m, qty.Defect);
        Assert.Equal(3m, qty.Scrap);
        Assert.Equal(2m, qty.Rework);
        Assert.Equal(100m, qty.Inspected);
    }
}
