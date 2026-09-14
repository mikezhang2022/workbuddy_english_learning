namespace FactoryReport.Application.Common;

/// <summary>
/// 报表查询参数校验失败。API 层映射为 RFC 7807 ProblemDetails（400）。
/// </summary>
public sealed class ReportQueryValidationException : Exception
{
    public string Title { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ReportQueryValidationException(
        string detail,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string title = "One or more validation errors occurred.")
        : base(detail)
    {
        Title = title;
        Errors = errors ?? new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    public static ReportQueryValidationException ForField(string fieldName, string message) =>
        new(
            detail: message,
            errors: new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [fieldName] = [message]
            });
}
