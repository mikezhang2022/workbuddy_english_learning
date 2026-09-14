using System.Net;

namespace FactoryReport.Client.Services.Api;

/// <summary>
/// 统一处理 HTTP 响应、ProblemDetails 与 Correlation ID。
/// </summary>
public static class ApiResponseHandler
{
    public static async Task EnsureSuccessOrThrowAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var problem = ApiProblemDetailsParser.TryParse(body)
                      ?? new ApiProblemDetails
                      {
                          Status = (int)response.StatusCode,
                          Title = response.ReasonPhrase,
                          Detail = body
                      };

        if (string.IsNullOrWhiteSpace(problem.CorrelationId)
            && response.Headers.TryGetValues(ApiConstants.CorrelationIdHeaderName, out var values))
        {
            problem.CorrelationId = values.FirstOrDefault();
        }

        if (problem.Status is null or 0)
        {
            problem.Status = (int)response.StatusCode;
        }

        // 登录失败统一提示，不暴露账号是否存在。
        if (response.StatusCode == HttpStatusCode.Unauthorized
            && response.RequestMessage?.RequestUri?.AbsolutePath.Contains("/auth/login", StringComparison.OrdinalIgnoreCase) == true)
        {
            problem.Detail = ApiConstants.LoginInvalidCredentialsMessage;
        }

        throw new ApiRequestException(problem);
    }

    public static string? ReadCorrelationId(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(ApiConstants.CorrelationIdHeaderName, out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    public static string? ReadAntiforgeryToken(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(ApiConstants.AntiforgeryHeaderName, out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    public static void ApplyCorrelationId(HttpRequestMessage request, string? correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation(ApiConstants.CorrelationIdHeaderName, correlationId);
        }
    }

    public static void ApplyAntiforgeryToken(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.TryAddWithoutValidation(ApiConstants.AntiforgeryHeaderName, token);
        }
    }
}
