using System.Text.Json;

namespace FactoryReport.Client.Services.Api;

/// <summary>
/// 解析 API 返回的 ProblemDetails JSON。
/// </summary>
public static class ApiProblemDetailsParser
{
    public static ApiProblemDetails? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var problem = new ApiProblemDetails
            {
                Status = root.TryGetProperty("status", out var status) ? status.GetInt32() : null,
                Title = root.TryGetProperty("title", out var title) ? title.GetString() : null,
                Detail = root.TryGetProperty("detail", out var detail) ? detail.GetString() : null
            };

            if (root.TryGetProperty("correlationId", out var correlationId))
            {
                problem.CorrelationId = correlationId.GetString();
            }

            if (root.TryGetProperty("traceId", out var traceId))
            {
                problem.TraceId = traceId.GetString();
            }

            return problem;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ApiProblemDetails NetworkError(string? correlationId = null)
        => new()
        {
            Status = null,
            Title = "Network Error",
            Detail = "无法连接服务器，请检查网络后重试。",
            CorrelationId = correlationId
        };
}
