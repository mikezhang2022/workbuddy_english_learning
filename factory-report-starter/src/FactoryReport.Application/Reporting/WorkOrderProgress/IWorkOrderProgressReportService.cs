namespace FactoryReport.Application.Reporting.WorkOrderProgress;

/// <summary>
/// 工单进度（work_order_progress）只读查询服务。
/// 仅通过 <c>IReportDataQueryService</c> 读取数据，不依赖 Fake 实现类。
/// </summary>
public interface IWorkOrderProgressReportService
{
    Task<WorkOrderProgressQueryResponse> QueryAsync(
        WorkOrderProgressQueryRequest request,
        CancellationToken cancellationToken = default);
}
