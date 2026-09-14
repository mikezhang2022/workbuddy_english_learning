namespace FactoryReport.Client.Services.Api;

/// <summary>
/// RFC 7807 ProblemDetails 子集（与 API 响应对齐）。
/// </summary>
public sealed class ApiProblemDetails
{
    public int? Status { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? CorrelationId { get; set; }
    public string? TraceId { get; set; }

    public string UserFacingMessage =>
        Status switch
        {
            401 => ApiConstants.LoginInvalidCredentialsMessage,
            403 => ApiConstants.ForbiddenScopeMessage,
            _ => string.IsNullOrWhiteSpace(Detail)
                ? "请求失败，请稍后重试。"
                : Detail
        };
}
