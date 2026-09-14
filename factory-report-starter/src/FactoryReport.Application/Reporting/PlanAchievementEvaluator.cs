using FactoryReport.Domain.Reporting;

namespace FactoryReport.Application.Reporting;

/// <summary>
/// 计划达成应用服务：封装已确认边界规则，供后续 Fake/报表引擎调用。
/// 不引入 MES/Oracle 依赖。
/// </summary>
public sealed class PlanAchievementEvaluator
{
    public PlanAchievementResult Evaluate(
        PlanAchievementKey key,
        decimal? planQuantity,
        decimal? actualQuantity) =>
        PlanAchievementResult.Calculate(key, planQuantity, actualQuantity);
}
