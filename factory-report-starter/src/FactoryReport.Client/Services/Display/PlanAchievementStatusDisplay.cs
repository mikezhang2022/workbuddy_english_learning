namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 计划达成 PlanStatus 中文展示。不重算达成率；保留原始状态供诊断。
/// API 状态值：Calculated / PlanIsZero / PlanNotConfigured / MissingActual。
/// </summary>
public static class PlanAchievementStatusDisplay
{
    public const string Calculated = "Calculated";
    public const string PlanIsZero = "PlanIsZero";
    public const string PlanNotConfigured = "PlanNotConfigured";
    public const string MissingActual = "MissingActual";

    public const string CalculatedLabel = "正常计算";
    public const string PlanIsZeroLabel = "计划为 0";
    public const string PlanNotConfiguredLabel = "未配置计划";
    public const string MissingActualLabel = "有计划但无实际";
    public const string UnknownLabel = "未知状态";

    /// <summary>
    /// 将 API 返回的 planStatus 转为中文文案；未知值返回「未知状态」。
    /// </summary>
    public static string GetChineseLabel(string? planStatus)
    {
        if (string.IsNullOrWhiteSpace(planStatus))
        {
            return UnknownLabel;
        }

        if (string.Equals(planStatus, Calculated, StringComparison.Ordinal))
        {
            return CalculatedLabel;
        }

        if (string.Equals(planStatus, PlanIsZero, StringComparison.Ordinal))
        {
            return PlanIsZeroLabel;
        }

        if (string.Equals(planStatus, PlanNotConfigured, StringComparison.Ordinal))
        {
            return PlanNotConfiguredLabel;
        }

        if (string.Equals(planStatus, MissingActual, StringComparison.Ordinal))
        {
            return MissingActualLabel;
        }

        return UnknownLabel;
    }

    /// <summary>
    /// 中文文案 + 原始状态（诊断/测试用）。
    /// </summary>
    public static string FormatWithRaw(string? planStatus)
    {
        var raw = string.IsNullOrWhiteSpace(planStatus) ? "—" : planStatus.Trim();
        return $"{GetChineseLabel(planStatus)}（{raw}）";
    }

    /// <summary>
    /// 达成率展示：始终使用 API 返回值，不按状态重算。
    /// PlanNotConfigured / PlanIsZero 的 null 显示「—」，不得显示 0%。
    /// </summary>
    public static string FormatAchievementRate(decimal? achievementRate)
        => RatioDisplayFormatter.FormatPercent(achievementRate);

    /// <summary>
    /// 计划数量：null（未配置计划）显示「—」。
    /// </summary>
    public static string FormatPlanQuantity(decimal? planQuantity)
        => planQuantity is null ? "—" : planQuantity.Value.ToString("0.##");
}
