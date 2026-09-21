using FactoryReport.Application.Import;
using FactoryReport.Domain.Import;
using FactoryReport.Domain.MasterData;
using FactoryReport.Domain.Organizations;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.UnitTests.Import;

public class ImportRowValidatorTests
{
    private readonly FakeFixtureSnapshot _snapshot = DeterministicFakeFixture.Create();

    private IReadOnlyList<Factory> Factories => _snapshot.Factories.Where(f => f.Id == 1).ToList();
    private IReadOnlyList<Workshop> Workshops => _snapshot.Workshops.Where(w => w.FactoryId == 1).ToList();
    private IReadOnlyList<ProductionLine> Lines => _snapshot.ProductionLines.Where(l => l.FactoryId == 1).ToList();
    private IReadOnlyList<Product> Products => _snapshot.Products.Where(p => p.FactoryId == 1).ToList();

    private static Dictionary<string, string?> PlanRow(
        string factory = "F-DEMO-01",
        string workshop = "W-DEMO-A",
        string? line = "L-A1",
        string month = "2026-03",
        string date = "2026-03-15",
        string product = "PROD-NORMAL",
        string qty = "10",
        string? remark = null)
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["factoryCode"] = factory,
            ["workshopCode"] = workshop,
            ["productionLineCode"] = line,
            ["planYearMonth"] = month,
            ["productionDate"] = date,
            ["productCode"] = product,
            ["planQuantity"] = qty,
            ["remark"] = remark
        };

    [Fact]
    public void Validate_Detects_Required_Type_Date_Org_Product_Negative_Duplicate()
    {
        var emptyQty = PlanRow();
        emptyQty["planQuantity"] = null;
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            emptyQty,
            PlanRow(date: "not-a-date", product: "PROD-NORMAL", qty: "1"),
            PlanRow(date: "1999-01-01", qty: "1"),
            PlanRow(factory: "F-NOPE", qty: "1"),
            PlanRow(product: "PROD-MISSING", qty: "1"),
            PlanRow(qty: "-5"),
            PlanRow(date: "2026-03-16", qty: "1"),
            PlanRow(date: "2026-03-16", qty: "2") // duplicate of previous
        };

        var errors = ImportRowValidator.Validate(
            ImportDatasetCodes.PlanData,
            rows,
            Factories,
            Workshops,
            Lines,
            Products);

        Assert.Contains(errors, e => e.Code == ImportRowValidator.CodeRequired && e.ColumnName == "planQuantity");
        Assert.Contains(errors, e => e.Code == ImportRowValidator.CodeDateFormat);
        Assert.Contains(errors, e => e.Code == ImportRowValidator.CodeDateRange);
        Assert.Contains(errors, e => e.Code == ImportRowValidator.CodeOrgNotFound);
        Assert.Contains(errors, e => e.Code == ImportRowValidator.CodeProductNotFound);
        Assert.Contains(errors, e => e.Code == ImportRowValidator.CodeNegative);
        Assert.Contains(errors, e => e.Code == ImportRowValidator.CodeDuplicate);
        Assert.All(errors, e =>
        {
            Assert.True(e.RowNumber >= 2);
            Assert.False(string.IsNullOrWhiteSpace(e.ColumnName));
            Assert.False(string.IsNullOrWhiteSpace(e.Reason));
        });
    }

    [Fact]
    public void Validate_CleanPlanRow_HasNoErrors()
    {
        var errors = ImportRowValidator.Validate(
            ImportDatasetCodes.PlanData,
            [PlanRow()],
            Factories,
            Workshops,
            Lines,
            Products);
        Assert.Empty(errors);
    }
}

public class ImportBatchTransitionTests
{
    [Fact]
    public void ReadyToPublish_RequiresSucceededZeroErrors()
    {
        var created = DeterministicFakeFixture.FixedUpdatedAtUtc;
        var draft = new ImportBatch(
            Guid.NewGuid(), 1, ImportDatasetCodes.PlanData, ImportBatchStatus.Pending, created);
        Assert.False(ImportBatchTransitions.IsReadyToPublish(draft));

        var ready = draft.WithStatus(
            ImportBatchStatus.Succeeded,
            totalRows: 2,
            errorRows: 0,
            completedAtUtc: created);
        Assert.True(ImportBatchTransitions.IsReadyToPublish(ready));

        var failed = draft.WithStatus(ImportBatchStatus.Failed, totalRows: 2, errorRows: 1, completedAtUtc: created);
        Assert.Throws<InvalidOperationException>(() => ImportBatchTransitions.EnsureCanPublish(failed));
    }

    [Fact]
    public void Published_CanRollback_Only()
    {
        var created = DeterministicFakeFixture.FixedUpdatedAtUtc;
        var published = new ImportBatch(
            Guid.NewGuid(),
            1,
            ImportDatasetCodes.PlanData,
            ImportBatchStatus.Succeeded,
            created,
            DatasetPublishStatus.Published,
            totalRows: 1,
            errorRows: 0,
            completedAtUtc: created);
        Assert.True(ImportBatchTransitions.IsPublished(published));
        ImportBatchTransitions.EnsureCanRollback(published);

        var draft = published.WithStatus(ImportBatchStatus.Succeeded, DatasetPublishStatus.Draft);
        Assert.Throws<InvalidOperationException>(() => ImportBatchTransitions.EnsureCanRollback(draft));
    }
}
