using FactoryReport.Application.Reporting;
using FactoryReport.Domain.Reporting;

namespace FactoryReport.UnitTests.Domain;

public class ReportCodesTests
{
    [Fact]
    public void Five_stable_report_codes_exist_with_fixed_values()
    {
        Assert.Equal(5, ReportCodes.All.Count);
        Assert.Equal("production_daily", ReportCodes.ProductionDaily);
        Assert.Equal("work_order_progress", ReportCodes.WorkOrderProgress);
        Assert.Equal("quality_statistics", ReportCodes.QualityStatistics);
        Assert.Equal("production_plan_achievement", ReportCodes.ProductionPlanAchievement);
        Assert.Equal("monthly_production_plan", ReportCodes.MonthlyProductionPlan);
    }

    [Fact]
    public void Application_stable_codes_delegate_to_domain_constants()
    {
        Assert.Equal(ReportCodes.All, StableReportCodes.All);
        Assert.True(StableReportCodes.IsKnown("production_daily"));
        Assert.False(StableReportCodes.IsKnown("unknown_report"));
    }

    [Fact]
    public void Known_codes_are_ordinal_exact()
    {
        Assert.True(ReportCodes.IsKnown("production_plan_achievement"));
        Assert.False(ReportCodes.IsKnown("PRODUCTION_DAILY"));
        Assert.False(ReportCodes.IsKnown("plan_achievement"));
    }
}
