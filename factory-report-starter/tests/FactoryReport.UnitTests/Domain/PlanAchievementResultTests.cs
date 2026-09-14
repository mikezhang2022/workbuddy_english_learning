using FactoryReport.Domain.Reporting;

namespace FactoryReport.UnitTests.Domain;

public class PlanAchievementResultTests
{
    private static PlanAchievementKey Key() =>
        new(factoryId: 1, workshopId: 2, productionLineId: 3, productionDate: new DateOnly(2026, 9, 1), productCode: "P-100");

    [Fact]
    public void Plan_zero_yields_null_achievement_rate()
    {
        var result = PlanAchievementResult.Calculate(Key(), planQuantity: 0m, actualQuantity: 50m);

        Assert.Equal(PlanAchievementStatus.PlanIsZero, result.Status);
        Assert.Null(result.AchievementRate);
        Assert.Equal(0m, result.PlanQuantity);
        Assert.Equal(50m, result.ActualQuantity);
    }

    [Fact]
    public void Actual_without_plan_is_plan_not_configured()
    {
        var result = PlanAchievementResult.Calculate(Key(), planQuantity: null, actualQuantity: 80m);

        Assert.Equal(PlanAchievementStatus.PlanNotConfigured, result.Status);
        Assert.Null(result.AchievementRate);
        Assert.Null(result.PlanQuantity);
        Assert.Equal(80m, result.ActualQuantity);
    }

    [Fact]
    public void Plan_without_actual_uses_zero_actual_and_zero_rate()
    {
        var result = PlanAchievementResult.Calculate(Key(), planQuantity: 100m, actualQuantity: null);

        Assert.Equal(PlanAchievementStatus.MissingActual, result.Status);
        Assert.Equal(0m, result.ActualQuantity);
        Assert.Equal(0m, result.AchievementRate);
        Assert.Equal(100m, result.PlanQuantity);
    }

    [Fact]
    public void Both_present_calculates_rate()
    {
        var result = PlanAchievementResult.Calculate(Key(), planQuantity: 100m, actualQuantity: 80m);

        Assert.Equal(PlanAchievementStatus.Calculated, result.Status);
        Assert.Equal(0.8m, result.AchievementRate);
        Assert.Equal(80m, result.ActualQuantity);
    }
}
