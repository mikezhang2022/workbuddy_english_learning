using FactoryReport.Application.Reporting.ProductionDaily;

namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 当前筛选返回行的前端展示汇总（非正式统计口径；以后端/API 为准）。
/// </summary>
public sealed class ProductionDailyDisplaySummary
{
    public decimal ActualQuantity { get; init; }
    public decimal GoodQuantity { get; init; }
    public decimal DefectQuantity { get; init; }
    public decimal ScrapQuantity { get; init; }
    public decimal ReworkQuantity { get; init; }
    public decimal InspectionQuantity { get; init; }
    public int RowCount { get; init; }

    public static ProductionDailyDisplaySummary FromRows(IReadOnlyList<ProductionDailyReportRow>? rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return new ProductionDailyDisplaySummary();
        }

        return new ProductionDailyDisplaySummary
        {
            RowCount = rows.Count,
            ActualQuantity = rows.Sum(r => r.ActualQuantity),
            GoodQuantity = rows.Sum(r => r.GoodQuantity),
            DefectQuantity = rows.Sum(r => r.DefectQuantity),
            ScrapQuantity = rows.Sum(r => r.ScrapQuantity),
            ReworkQuantity = rows.Sum(r => r.ReworkQuantity),
            InspectionQuantity = rows.Sum(r => r.InspectionQuantity)
        };
    }
}
