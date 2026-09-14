using System.Security.Claims;
using FactoryReport.Api.Middleware;
using FactoryReport.Api.Security;
using FactoryReport.Application.Security;
using FactoryReport.Application.Security.DataScope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Endpoints;

/// <summary>
/// 本地账号 Cookie 认证端点（阶段 10）。
/// POST /api/v1/auth/login | POST /api/v1/auth/logout | GET /api/v1/auth/me
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync)
            .WithName("AuthLogin")
            .WithSummary("本地账号密码登录，签发安全 HttpOnly Cookie")
            .AllowAnonymous()
            .Produces<AuthMeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/logout", LogoutAsync)
            .WithName("AuthLogout")
            .WithSummary("退出登录并清除会话 Cookie（需 Antiforgery）")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/me", MeAsync)
            .WithName("AuthMe")
            .WithSummary("当前会话用户；未登录返回 401")
            .AllowAnonymous()
            .Produces<AuthMeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/csrf", CsrfAsync)
            .WithName("AuthCsrf")
            .WithSummary("为已登录会话签发 Antiforgery 请求令牌（响应头 X-CSRF-TOKEN）")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest? request,
        HttpContext httpContext,
        ILocalAccountAuthenticationService authService,
        IUserScopeResolver userScopeResolver,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("FactoryReport.Auth");

        if (request is null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrEmpty(request.Password))
        {
            // 与凭证错误统一，避免枚举。不记录密码或请求体。
            logger.LogInformation(
                "Login rejected (invalid payload). CorrelationId={CorrelationId} Path={Path}",
                httpContext.GetCorrelationId(),
                httpContext.Request.Path.Value);
            return UnauthorizedLogin();
        }

        var result = await authService
            .AuthenticateAsync(request.UserName, request.Password, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded || result.User is null)
        {
            logger.LogInformation(
                "Login failed. CorrelationId={CorrelationId} UserName={UserName}",
                httpContext.GetCorrelationId(),
                request.UserName.Trim());
            return UnauthorizedLogin();
        }

        var identity = new ClaimsIdentity(
            result.Claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            }).ConfigureAwait(false);

        // SignInAsync 只写 Cookie，不更新当前请求的 User；Antiforgery 令牌绑定身份，必须先对齐。
        httpContext.User = principal;

        // 登录成功后签发 CSRF 令牌，供 logout / 未来写入端点使用。Cookie 不含密码或权限明细。
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (!string.IsNullOrEmpty(tokens.RequestToken))
        {
            httpContext.Response.Headers[FactoryReportAuthDefaults.AntiforgeryHeaderName] =
                tokens.RequestToken;
        }

        logger.LogInformation(
            "Login succeeded. CorrelationId={CorrelationId} UserId={UserId}",
            httpContext.GetCorrelationId(),
            result.User.UserId);

        var scope = await userScopeResolver
            .ResolveAsync(result.User.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(AuthMeResponse.From(result.User, DataScopeSummary.From(scope)));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("FactoryReport.Auth");

        try
        {
            await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(
                ex,
                "Logout rejected: antiforgery validation failed. CorrelationId={CorrelationId} Message={Message}",
                httpContext.GetCorrelationId(),
                ex.Message);
            return Results.Problem(
                detail: "Antiforgery token validation failed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
            .ConfigureAwait(false);

        logger.LogInformation(
            "Logout completed. CorrelationId={CorrelationId}",
            httpContext.GetCorrelationId());

        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        ICurrentUserAccessor currentUserAccessor,
        IUserScopeResolver userScopeResolver,
        CancellationToken cancellationToken)
    {
        var user = currentUserAccessor.GetCurrentUser();
        if (user is null)
        {
            return Results.Problem(
                detail: "Not authenticated.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        var scope = await userScopeResolver
            .ResolveAsync(user.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(AuthMeResponse.From(user, DataScopeSummary.From(scope)));
    }

    private static IResult CsrfAsync(HttpContext httpContext, IAntiforgery antiforgery)
    {
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return Results.Problem(
                detail: "Not authenticated.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");
        }

        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (!string.IsNullOrEmpty(tokens.RequestToken))
        {
            httpContext.Response.Headers[FactoryReportAuthDefaults.AntiforgeryHeaderName] =
                tokens.RequestToken;
        }

        return Results.NoContent();
    }

    private static IResult UnauthorizedLogin()
        => Results.Problem(
            detail: LocalAccountAuthenticationService.FailureMessage,
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized");
}

public sealed class LoginRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

public sealed class AuthMeResponse
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public bool IsAuthenticated { get; init; }
    public DataScopeSummary? DataScope { get; init; }

    public static AuthMeResponse From(CurrentUser user, DataScopeSummary? dataScope = null)
        => new()
        {
            UserId = user.UserId,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Roles = user.Roles,
            IsAuthenticated = user.IsAuthenticated,
            DataScope = dataScope
        };
}
