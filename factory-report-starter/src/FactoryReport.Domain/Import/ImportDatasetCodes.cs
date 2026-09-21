using FactoryReport.Domain.Reporting;

namespace FactoryReport.Domain.Import;

/// <summary>
/// Excel 导入数据集编码。计划复用既有月度计划编码；实际为导入专用编码（不写入 Oracle 业务表）。
/// </summary>
public static class ImportDatasetCodes
{
    /// <summary>计划数据（对齐 <see cref="ReportCodes.MonthlyProductionPlan"/>）。</summary>
    public const string PlanData = ReportCodes.MonthlyProductionPlan;

    /// <summary>实际数据（导入对比用；与 MES Oracle 业务表职责分离）。</summary>
    public const string ActualData = "production_actual";

    public static IReadOnlyList<string> All { get; } = [PlanData, ActualData];

    public static bool IsKnown(string? datasetCode)
    {
        if (string.IsNullOrWhiteSpace(datasetCode))
        {
            return false;
        }

        var code = datasetCode.Trim();
        return All.Any(c => string.Equals(c, code, StringComparison.Ordinal));
    }

    public static string DisplayName(string datasetCode) => datasetCode.Trim() switch
    {
        PlanData => "计划数据",
        ActualData => "实际数据",
        _ => datasetCode
    };
}
