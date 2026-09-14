using FactoryReport.Application.Reporting.MonthlyProductionPlan;
using FactoryReport.Domain.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Endpoints;

/// <summary>
/// 月度生产计划只读查询端点：GET /api/v1/reports/monthly-production-plan。
/// 不提供任何写入端点（无 Excel 上传/发布/回退）。
/// </summary>
public static class MonthlyProductionPlanReportEndpoints
{
    public static IEndpointRouteBuilder MapMonthlyProductionPlanReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/monthly-production-plan", QueryMonthlyProductionPlanAsync)
            .WithName("GetMonthlyProductionPlanReport")
            .WithTags("Reports")
            .WithSummary("查询月度生产计划（monthly_production_plan）日计划行")
            .WithDescription(
                "按 factoryId + planMonth（yyyy-MM）返回月度文件内的日计划行（保留 PlanDate）。" +
                "不得将月计划平均推算到每天。" +
                "Fake 版本行为：仅返回 Published 且 Active 的计划版本；" +
                "真实 Excel 发布/激活/回退【待现场确认】。只读，无写入。")
            .Produces<MonthlyProductionPlanQueryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AuthorizationPolicies.ReportRead);

        return app;
    }

    private static async Task<Ok<MonthlyProductionPlanQueryResponse>> QueryMonthlyProductionPlanAsync(
        [FromQuery] long? factoryId,
        [FromQuery] string? planMonth,
        [FromQuery] long? workshopId,
        [FromQuery] long? productionLineId,
        [FromQuery] string? productCode,
        IMonthlyProductionPlanReportService reportService,
        CancellationToken cancellationToken)
    {
        var request = new MonthlyProductionPlanQueryRequest
        {
            FactoryId = factoryId,
            PlanMonth = planMonth,
            WorkshopId = workshopId,
            ProductionLineId = productionLineId,
            ProductCode = productCode
        };

        var response = await reportService.QueryAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(response);
    }
}
