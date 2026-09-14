using System.Globalization;

namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 计划月份 <c>yyyy-MM</c> 校验（与 API 严格格式对齐；不伪造当前月）。
/// </summary>
public static class PlanMonthFormat
{
    public const string RequiredError = "请填写计划月份（yyyy-MM）。";
    public const string InvalidFormatError = "计划月份格式须为 yyyy-MM。";

    /// <summary>
    /// 严格校验：长度 7、中间为 '-'、四位年、两位月（01–12）。
    /// </summary>
    public static bool TryNormalize(string? input, out string planMonth)
    {
        planMonth = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();
        if (value.Length != 7 || value[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month))
        {
            return false;
        }

        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            return false;
        }

        planMonth = value;
        return true;
    }

    /// <summary>判断 PlanDate 是否落在给定 yyyy-MM 自然月内。</summary>
    public static bool BelongsToPlanMonth(DateOnly planDate, string planMonth)
    {
        if (!TryNormalize(planMonth, out var normalized))
        {
            return false;
        }

        var year = int.Parse(normalized.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var month = int.Parse(normalized.AsSpan(5, 2), CultureInfo.InvariantCulture);
        return planDate.Year == year && planDate.Month == month;
    }
}
