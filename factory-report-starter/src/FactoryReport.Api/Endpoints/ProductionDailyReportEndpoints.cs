using FactoryReport.Application.Reporting.ProductionDaily;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Endpoints;

/// <summary>
/// 生产日报只读查询端点：GET /api/v1/reports/production-daily。
/// 不提供任何写入端点。
/// </summary>
public static class ProductionDailyReportEndpoints
{
    public static IEndpointRouteBuilder MapProductionDailyReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/production-daily", QueryProductionDailyAsync)
            .WithName("GetProductionDailyReport")
            .WithTags("Reports")
            .WithSummary("查询生产日报（production_daily）聚合结果")
            .WithDescription(
                "按生产日期 + 组织范围 + 产品聚合 Fake/MES 生产事实。" +
                "良率公式为 Fake 临时口径：YieldRate = GoodQuantity / InspectionQuantity；" +
                "InspectionQuantity 为 0 时 YieldRate 为 null。" +
                "『Fake 测试口径，现场 MES 接入前须确认』。只读，无写入。")
            .Produces<ProductionDailyQueryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    /// <summary>
    /// 生产日报查询。查询参数使用明确日期语义（DateOnly / yyyy-MM-dd）。
    /// </summary>
    private static async Task<Ok<ProductionDailyQueryResponse>> QueryProductionDailyAsync(
        [FromQuery] long? factoryId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] long? workshopId,
        [FromQuery] long? productionLineId,
        [FromQuery] string? productCode,
        IProductionDailyReportService reportService,
        CancellationToken cancellationToken)
    {
        var request = new ProductionDailyQueryRequest
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
