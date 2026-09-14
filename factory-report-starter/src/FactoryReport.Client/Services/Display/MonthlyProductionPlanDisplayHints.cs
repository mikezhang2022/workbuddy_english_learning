namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 月度生产计划页面固定提示文案（日行原则与 Fake 版本行为）。
/// </summary>
public static class MonthlyProductionPlanDisplayHints
{
    /// <summary>强调按月上传、按日计划行展示，禁止前端月均摊。</summary>
    public const string DailyPlanLinePrincipleZh =
        "本页按月上传、按日计划行展示原始 PlanDate 与 PlanQuantity；不得在前端将月计划平均或推算到每天。";

    /// <summary>Fake：仅 Published 且 Active 的计划版本可见。</summary>
    public const string FakePublishedActiveOnlyZh =
        "仅展示 Published 且 Active 的 Fake 计划版本；Draft 等非激活版本不会返回。";

    /// <summary>正式 Excel 发布/激活/回退规则后续阶段实现。</summary>
    public const string FormalVersionRulesPendingZh =
        "正式 Excel 发布、激活和回退规则将在后续阶段实现与确认【待现场确认】。";
}
