using System.Diagnostics;
using FactoryReport.Api.Middleware;
using FactoryReport.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Infrastructure;

/// <summary>
/// 将未处理异常映射为 RFC 7807 ProblemDetails。生产环境不返回堆栈。
/// 校验类异常（报表查询）映射为 400。
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.GetCorrelationId()
            ?? Activity.Current?.Id
            ?? httpContext.TraceIdentifier;

        var isValidation = exception is ReportQueryValidationException;

        if (isValidation)
        {
            logger.LogWarning(
                exception,
                "Report query validation failed. CorrelationId={CorrelationId} Path={Path} Method={Method}",
                correlationId,
                httpContext.Request.Path.Value,
                httpContext.Request.Method);
        }
        else
        {
            // 不记录请求体、Authorization、Cookie、连接串等敏感信息。
            logger.LogError(
                exception,
                "Unhandled exception. CorrelationId={CorrelationId} Path={Path} Method={Method}",
                correlationId,
                httpContext.Request.Path.Value,
                httpContext.Request.Method);
        }

        ProblemDetails problem;
        if (exception is ReportQueryValidationException validation)
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = validation.Title,
                Type = "https://tools.ietf.org/html/rfc7807",
                Instance = httpContext.Request.Path.Value,
                Detail = validation.Message
            };
            problem.Extensions["errors"] = validation.Errors;
        }
        else
        {
            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7807",
                Instance = httpContext.Request.Path.Value,
                Detail = environment.IsDevelopment() || environment.IsEnvironment("Testing")
                    ? exception.Message
                    : "An unexpected error occurred. Use the correlation id when contacting support."
            };
        }

        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            problem.Extensions["exceptionType"] = exception.GetType().FullName;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });

        return true;
    }
}
