using FactoryReport.Application.Reporting.QualityStatistics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Endpoints;

/// <summary>
/// 质量统计只读查询端点：GET /api/v1/reports/quality-statistics。
/// 不提供任何写入端点。
/// </summary>
public static class QualityStatisticsReportEndpoints
{
    public static IEndpointRouteBuilder MapQualityStatisticsReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/quality-statistics", QueryQualityStatisticsAsync)
            .WithName("GetQualityStatisticsReport")
            .WithTags("Reports")
            .WithSummary("查询质量统计（quality_statistics）聚合结果")
            .WithDescription(
                "按生产日期 + 组织范围 + 产品聚合质量数量。" +
                "Fake 临时口径：YieldRate = GoodQuantity / InspectionQuantity；" +
                "DefectRate = DefectQuantity / InspectionQuantity；" +
                "InspectionQuantity 为 0 时两种比率均为 null；" +
                "Good/Defect/Scrap/Rework 分列，不把报废或返工自动合并到不良。" +
                "『Fake 测试口径，现场 MES 接入前须确认』。只读，无写入。" +
                "不支持 workOrderCode：ProductionRecord 无工单维度。")
            .Produces<QualityStatisticsQueryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    /// <summary>
    /// 质量统计查询。查询参数使用明确日期语义（DateOnly / yyyy-MM-dd）。
    /// </summary>
    private static async Task<Ok<QualityStatisticsQueryResponse>> QueryQualityStatisticsAsync(
        [FromQuery] long? factoryId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] long? workshopId,
        [FromQuery] long? productionLineId,
        [FromQuery] string? productCode,
        IQualityStatisticsReportService reportService,
        CancellationToken cancellationToken)
    {
        var request = new QualityStatisticsQueryRequest
        {
            FactoryId = factoryId,
            StartDate = startDate,
            EndDate = endDate,
            WorkshopId = workshopId,
            ProductionLineId = productionLineId,
            ProductCode = productCode
        };

        var response = await reportService.QueryAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(response);
    }
}
