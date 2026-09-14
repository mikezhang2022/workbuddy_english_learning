namespace FactoryReport.Application.Reporting.ProductionPlanAchievement;

/// <summary>
/// 生产计划达成（production_plan_achievement）只读查询服务。
/// 仅通过 <c>IReportDataQueryService</c> 读取数据，不依赖 Fake 实现类。
/// </summary>
public interface IProductionPlanAchievementReportService
{
    Task<ProductionPlanAchievementQueryResponse> QueryAsync(
        ProductionPlanAchievementQueryRequest request,
        CancellationToken cancellationToken = default);
}
