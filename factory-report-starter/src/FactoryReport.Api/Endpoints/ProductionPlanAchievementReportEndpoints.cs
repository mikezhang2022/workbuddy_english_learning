using FactoryReport.Application.Reporting.ProductionPlanAchievement;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Endpoints;

/// <summary>
/// 生产计划达成只读查询端点：GET /api/v1/reports/production-plan-achievement。
/// 不提供任何写入端点。
/// </summary>
public static class ProductionPlanAchievementReportEndpoints
{
    public static IEndpointRouteBuilder MapProductionPlanAchievementReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/production-plan-achievement", QueryProductionPlanAchievementAsync)
            .WithName("GetProductionPlanAchievementReport")
            .WithTags("Reports")
            .WithSummary("查询生产计划达成（production_plan_achievement）组合结果")
            .WithDescription(
                "按关联键 FactoryId + WorkshopId + ProductionLineId + ProductionDate + ProductCode " +
                "匹配日计划与实际产量。" +
                "已确认规则：计划>0 时 AchievementRate=Actual/Plan；" +
                "计划为 0 → null（PlanIsZero）；" +
                "有实际无计划 → null（PlanNotConfigured，不得显示 0%）；" +
                "有计划无实际 → Actual=0、AchievementRate=0（MissingActual）；" +
                "不得用月计划平均推算日计划。只读，无写入。")
            .Produces<ProductionPlanAchievementQueryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    /// <summary>
    /// 生产计划达成查询。查询参数使用明确日期语义（DateOnly / yyyy-MM-dd）。
    /// </summary>
    private static async Task<Ok<ProductionPlanAchievementQueryResponse>> QueryProductionPlanAchievementAsync(
        [FromQuery] long? factoryId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] long? workshopId,
        [FromQuery] long? productionLineId,
        [FromQuery] string? productCode,
        IProductionPlanAchievementReportService reportService,
        CancellationToken cancellationToken)
    {
        var request = new ProductionPlanAchievementQueryRequest
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
