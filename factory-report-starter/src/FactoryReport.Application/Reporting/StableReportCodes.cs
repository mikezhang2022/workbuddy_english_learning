using FactoryReport.Domain.Reporting;

namespace FactoryReport.Application.Reporting;

/// <summary>
/// 应用层对稳定报表编码的只读访问入口（委托 Domain 常量，避免各层散落字符串）。
/// </summary>
public static class StableReportCodes
{
    public static string ProductionDaily => ReportCodes.ProductionDaily;
    public static string WorkOrderProgress => ReportCodes.WorkOrderProgress;
    public static string QualityStatistics => ReportCodes.QualityStatistics;
    public static string ProductionPlanAchievement => ReportCodes.ProductionPlanAchievement;
    public static string MonthlyProductionPlan => ReportCodes.MonthlyProductionPlan;

    public static IReadOnlyList<string> All => ReportCodes.All;

    public static bool IsKnown(string code) => ReportCodes.IsKnown(code);
}
