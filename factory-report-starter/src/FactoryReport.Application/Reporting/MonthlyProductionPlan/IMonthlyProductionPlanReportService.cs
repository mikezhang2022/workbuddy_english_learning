namespace FactoryReport.Application.Reporting.MonthlyProductionPlan;

/// <summary>
/// 月度生产计划（monthly_production_plan）只读查询服务。
/// 仅通过 <c>IReportDataQueryService</c> 读取数据，不依赖 Fake 实现类。
/// </summary>
public interface IMonthlyProductionPlanReportService
{
    Task<MonthlyProductionPlanQueryResponse> QueryAsync(
        MonthlyProductionPlanQueryRequest request,
        CancellationToken cancellationToken = default);
}
