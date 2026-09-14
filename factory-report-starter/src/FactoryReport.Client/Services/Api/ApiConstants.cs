namespace FactoryReport.Client.Services.Api;

public static class ApiConstants
{
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";
    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const string HttpClientName = "FactoryReportApi";

    public const string LoginInvalidCredentialsMessage = "用户名或密码不正确，请重试。";
    public const string LoginGenericFailureMessage = "登录失败，请稍后重试。";
    public const string ForbiddenScopeMessage = "无权访问该数据范围";
    public const string OfflineDataMessage = "当前离线，报表数据需联网获取。";
    public const string ReportOfflineMessage = "数据需联网获取";
    public const string DisplaySummaryDisclaimer =
        "以下汇总仅为当前筛选返回行的前端展示合计，正式统计口径以 API/后端为准。";
}
