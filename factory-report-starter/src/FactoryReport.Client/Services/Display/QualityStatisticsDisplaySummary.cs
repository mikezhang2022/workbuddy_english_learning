using FactoryReport.Application.Reporting.QualityStatistics;

namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 当前筛选返回行的前端展示汇总（非正式统计口径；以后端/API 为准）。
/// 仅合计数量分列；不重算良率/不良率；不把报废或返工并入不良。
/// </summary>
public sealed class QualityStatisticsDisplaySummary
{
    public decimal InspectionQuantity { get; init; }
    public decimal GoodQuantity { get; init; }
    public decimal DefectQuantity { get; init; }
    public decimal ScrapQuantity { get; init; }
    public decimal ReworkQuantity { get; init; }
    public int RowCount { get; init; }

    public static QualityStatisticsDisplaySummary FromRows(IReadOnlyList<QualityStatisticsReportRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return new QualityStatisticsDisplaySummary();
        }

        return new QualityStatisticsDisplaySummary
        {
            RowCount = rows.Count,
            InspectionQuantity = rows.Sum(r => r.InspectionQuantity),
            GoodQuantity = rows.Sum(r => r.GoodQuantity),
            DefectQuantity = rows.Sum(r => r.DefectQuantity),
            ScrapQuantity = rows.Sum(r => r.ScrapQuantity),
            ReworkQuantity = rows.Sum(r => r.ReworkQuantity)
        };
    }
}
