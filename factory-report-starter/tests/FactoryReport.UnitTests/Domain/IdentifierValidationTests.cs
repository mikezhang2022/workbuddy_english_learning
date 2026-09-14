using FactoryReport.Domain.Common;
using FactoryReport.Domain.MasterData;
using FactoryReport.Domain.Organizations;
using FactoryReport.Domain.Planning;
using FactoryReport.Domain.Reporting;

namespace FactoryReport.UnitTests.Domain;

public class IdentifierValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Factory_rejects_invalid_id(long id)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Factory(id, "F01", "Factory"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Factory_rejects_blank_code(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Factory(1, code!, "Factory"));
    }

    [Fact]
    public void Workshop_requires_positive_factory_id()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Workshop(1, 0, "W01", "Workshop"));
    }

    [Fact]
    public void ProductionLine_requires_positive_workshop_id()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductionLine(1, 1, 0, "L01", "Line"));
    }

    [Fact]
    public void Product_requires_product_code()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Product(1, 1, " ", "Name"));
    }

    [Fact]
    public void Daily_plan_line_requires_plan_date()
    {
        Assert.Throws<ArgumentException>(() =>
            new DailyProductionPlanLine(1, 1, default, "P-1", 10m, Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")));
    }

    [Fact]
    public void Daily_plan_line_rejects_blank_product_code()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new DailyProductionPlanLine(1, 1, new DateOnly(2026, 9, 1), "  ", 10m, Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")));
    }

    [Fact]
    public void Daily_plan_line_rejects_empty_plan_version_id()
    {
        Assert.Throws<ArgumentException>(() =>
            new DailyProductionPlanLine(1, 1, new DateOnly(2026, 9, 1), "P-1", 10m, Guid.Empty));
    }

    [Fact]
    public void Monthly_plan_rejects_invalid_year_month()
    {
        var versionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        Assert.Throws<ArgumentException>(() =>
            new MonthlyProductionPlan(
                1,
                1,
                "2026-13",
                versionId,
                [new DailyProductionPlanLine(1, 1, new DateOnly(2026, 9, 1), "P-1", 1m, versionId)],
                UtcInstant.Now()));
    }

    [Fact]
    public void Plan_achievement_key_rejects_invalid_ids_and_blank_product()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PlanAchievementKey(0, 1, 1, new DateOnly(2026, 9, 1), "P-1"));
        Assert.Throws<ArgumentException>(() =>
            new PlanAchievementKey(1, 1, 1, default, "P-1"));
        Assert.ThrowsAny<ArgumentException>(() =>
            new PlanAchievementKey(1, 1, 1, new DateOnly(2026, 9, 1), " "));
    }
}
