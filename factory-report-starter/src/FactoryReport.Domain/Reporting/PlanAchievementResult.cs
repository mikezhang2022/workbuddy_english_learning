using FactoryReport.Domain.Production;

namespace FactoryReport.Domain.Reporting;

/// <summary>
/// 计划达成计算状态（已确认边界规则）。
/// </summary>
public enum PlanAchievementStatus
{
    /// <summary>计划与实际均可用，可计算达成率。</summary>
    Calculated = 0,

    /// <summary>计划数量为 0：达成率为 null。</summary>
    PlanIsZero = 1,

    /// <summary>有实际无计划：未配置计划。</summary>
    PlanNotConfigured = 2,

    /// <summary>有计划无实际：实际按 0，达成率 0%。</summary>
    MissingActual = 3
}

/// <summary>
/// 计划达成结果。数量不在此混合重算业务口径；仅按已确认边界规则计算达成率。
/// </summary>
public sealed class PlanAchievementResult
{
    public PlanAchievementKey Key { get; }
    public decimal? PlanQuantity { get; }
    public decimal ActualQuantity { get; }
    public decimal? AchievementRate { get; }
    public PlanAchievementStatus Status { get; }

    private PlanAchievementResult(
        PlanAchievementKey key,
        decimal? planQuantity,
        decimal actualQuantity,
        decimal? achievementRate,
        PlanAchievementStatus status)
    {
        Key = key;
        PlanQuantity = planQuantity;
        ActualQuantity = actualQuantity;
        AchievementRate = achievementRate;
        Status = status;
    }

    /// <summary>
    /// 按已确认规则计算达成率。
    /// <list type="bullet">
    /// <item>计划为 0 → AchievementRate = null，Status = PlanIsZero</item>
    /// <item>有实际无计划 → Status = PlanNotConfigured（未配置计划），AchievementRate = null</item>
    /// <item>有计划无实际 → ActualQuantity = 0，AchievementRate = 0%，Status = MissingActual</item>
    /// </list>
    /// </summary>
    /// <param name="key">关联键。</param>
    /// <param name="planQuantity">计划数量；null 表示未配置计划。</param>
    /// <param name="actualQuantity">实际数量；null 表示无实际（按 0 处理当且仅当存在计划）。</param>
    public static PlanAchievementResult Calculate(
        PlanAchievementKey key,
        decimal? planQuantity,
        decimal? actualQuantity)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (planQuantity is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(planQuantity), "Plan quantity must not be negative.");
        }

        if (actualQuantity is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actualQuantity), "Actual quantity must not be negative.");
        }

        if (planQuantity is null)
        {
            var actual = actualQuantity ?? 0m;
            QuantityGuard.EnsureNonNegative(actual, nameof(actualQuantity));
            return new PlanAchievementResult(
                key,
                planQuantity: null,
                actualQuantity: actual,
                achievementRate: null,
                status: PlanAchievementStatus.PlanNotConfigured);
        }

        if (planQuantity.Value == 0m)
        {
            var actualWhenPlanZero = actualQuantity ?? 0m;
            return new PlanAchievementResult(
                key,
                planQuantity: 0m,
                actualQuantity: actualWhenPlanZero,
                achievementRate: null,
                status: PlanAchievementStatus.PlanIsZero);
        }

        if (actualQuantity is null)
        {
            return new PlanAchievementResult(
                key,
                planQuantity: planQuantity.Value,
                actualQuantity: 0m,
                achievementRate: 0m,
                status: PlanAchievementStatus.MissingActual);
        }

        var rate = actualQuantity.Value / planQuantity.Value;
        return new PlanAchievementResult(
            key,
            planQuantity: planQuantity.Value,
            actualQuantity: actualQuantity.Value,
            achievementRate: rate,
            status: PlanAchievementStatus.Calculated);
    }
}
