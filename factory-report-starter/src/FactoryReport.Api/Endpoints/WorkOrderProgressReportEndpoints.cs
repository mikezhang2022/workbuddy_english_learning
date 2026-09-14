using FactoryReport.Application.Reporting.WorkOrderProgress;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Endpoints;

/// <summary>
/// 工单进度只读查询端点：GET /api/v1/reports/work-order-progress。
/// 不提供任何写入端点。
/// </summary>
public static class WorkOrderProgressReportEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrderProgressReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reports/work-order-progress", QueryWorkOrderProgressAsync)
            .WithName("GetWorkOrderProgressReport")
            .WithTags("Reports")
            .WithSummary("查询工单进度（work_order_progress）")
            .WithDescription(
                "按工厂隔离返回工单计划/实际/完成率与 Fake 延期标记。" +
                "延期 Fake 规则：未完成且未关闭，且 UtcNow > PlannedFinishUtc。" +
                "『Fake 测试规则：现场须确认工单状态枚举、时区、延期口径与计划时间来源』。只读，无写入。")
            .Produces<WorkOrderProgressQueryResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<Ok<WorkOrderProgressQueryResponse>> QueryWorkOrderProgressAsync(
        [FromQuery] long? factoryId,
        [FromQuery] long? workshopId,
        [FromQuery] long? productionLineId,
        [FromQuery] string? productCode,
        [FromQuery] string? workOrderCode,
        [FromQuery] string? status,
        [FromQuery] DateOnly? plannedFinishFrom,
        [FromQuery] DateOnly? plannedFinishTo,
        IWorkOrderProgressReportService reportService,
        CancellationToken cancellationToken)
    {
        var request = new WorkOrderProgressQueryRequest
        {
            FactoryId = factoryId,
            WorkshopId = workshopId,
            ProductionLineId = productionLineId,
            ProductCode = productCode,
            WorkOrderCode = workOrderCode,
            Status = status,
            PlannedFinishFrom = plannedFinishFrom,
            PlannedFinishTo = plannedFinishTo
        };

        var response = await reportService.QueryAsync(request, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(response);
    }
}
