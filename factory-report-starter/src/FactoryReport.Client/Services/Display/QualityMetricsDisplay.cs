namespace FactoryReport.Client.Services.Display;

/// <summary>
/// 质量比率 Fake 口径提示。使用 API 返回的 YieldRate / DefectRate，前端不重算。
/// </summary>
public static class QualityMetricsDisplay
{
    /// <summary>
    /// 页面固定提示：质量比率为 Fake 测试口径，现场 MES 接入前须确认。
    /// </summary>
    public const string FakeRateHint = "质量比率为 Fake 测试口径，现场 MES 接入前须确认";

    /// <summary>
    /// 数量分列说明：报废/返工不得并入不良。
    /// </summary>
    public const string QuantityColumnsHint =
        "检验/良品/不良/报废/返工分列展示；不把报废或返工并入不良。";
}
