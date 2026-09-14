namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 良率等可空比率的展示格式化。
/// </summary>
public static class RatioDisplayFormatter
{
    /// <summary>
    /// null →「—」；非 null → 百分比（保留最多两位小数）。不得把 null 显示为 0%。
    /// </summary>
    public static string FormatPercent(decimal? rate)
    {
        if (rate is null)
        {
            return "—";
        }

        return $"{rate.Value * 100m:0.##}%";
    }
}
