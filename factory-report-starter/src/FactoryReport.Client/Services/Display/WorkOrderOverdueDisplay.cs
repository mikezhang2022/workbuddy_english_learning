namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 工单延期标记展示。使用 API 返回的 IsOverdue，前端不重算延期。
/// </summary>
public static class WorkOrderOverdueDisplay
{
    public const string OverdueLabel = "延期";
    public const string OverdueCssClass = "row-overdue";
    public const string FakeRuleHint =
        "延期判定为 Fake 临时规则（未完成且超过 PlannedFinishUtc），正式口径待现场确认。";

    /// <summary>
    /// IsOverdue=true 时返回醒目标签文案；否则 null（不展示标记）。
    /// </summary>
    public static string? GetBadgeText(bool isOverdue)
        => isOverdue ? OverdueLabel : null;

    public static string GetRowCssClass(bool isOverdue)
        => isOverdue ? OverdueCssClass : string.Empty;
}
