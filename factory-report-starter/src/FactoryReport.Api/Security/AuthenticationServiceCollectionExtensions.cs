using System.Security.Claims;
using FactoryReport.Application.Security;
using FactoryReport.Domain.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace FactoryReport.Api.Security;

/// <summary>
/// Cookie 认证、Antiforgery 与授权策略注册（阶段 10/11）。
/// 报表 API 使用 <see cref="AuthorizationPolicies.ReportRead"/>。
/// </summary>
public static class FactoryReportAuthDefaults
{
    public const string AuthCookieName = ".FactoryReport.Auth";
    public const string AntiforgeryCookieName = ".FactoryReport.Antiforgery";
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";
}

public static class FactoryReportAuthenticationExtensions
{
    public static IServiceCollection AddFactoryReportAuthentication(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = FactoryReportAuthDefaults.AuthCookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                // Production 必须 Secure；开发/测试允许 SameAsRequest 以便 HTTP 集成测试。
                options.Cookie.SecurePolicy = environment.IsProduction()
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.LoginPath = "/api/v1/auth/login";
                options.AccessDeniedPath = "/api/v1/auth/login";

                // API 不重定向到登录页，返回 401/403。
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.RequireAuthenticated,
                policy => policy.RequireAuthenticatedUser());

            options.AddPolicy(
                AuthorizationPolicies.RequireSystemAdmin,
                policy => policy.RequireRole(AppRoles.SystemAdmin));

            options.AddPolicy(
                AuthorizationPolicies.RequireFactoryAdmin,
                policy => policy.RequireRole(AppRoles.FactoryAdmin));

            options.AddPolicy(
                AuthorizationPolicies.RequireProductionManager,
                policy => policy.RequireRole(AppRoles.ProductionManager));

            options.AddPolicy(
                AuthorizationPolicies.RequireQualityUser,
                policy => policy.RequireRole(AppRoles.QualityUser));

            options.AddPolicy(
                AuthorizationPolicies.RequireViewer,
                policy => policy.RequireRole(AppRoles.Viewer));

            // 阶段 11：五个角色均可 ReportRead；角色×报表细粒度矩阵【待后续确认】。
            options.AddPolicy(
                AuthorizationPolicies.ReportRead,
                policy => policy.RequireRole(
                    AppRoles.SystemAdmin,
                    AppRoles.FactoryAdmin,
                    AppRoles.ProductionManager,
                    AppRoles.QualityUser,
                    AppRoles.Viewer));

            options.AddPolicy(
                AuthorizationPolicies.ImportRead,
                policy => policy.RequireRole(
                    AppRoles.SystemAdmin,
                    AppRoles.FactoryAdmin,
                    AppRoles.ProductionManager,
                    AppRoles.QualityUser,
                    AppRoles.Viewer));

            options.AddPolicy(
                AuthorizationPolicies.ImportManage,
                policy => policy.RequireRole(
                    AppRoles.SystemAdmin,
                    AppRoles.FactoryAdmin));

            options.AddPolicy(
                AuthorizationPolicies.CanViewProductionReports,
                policy => policy.RequireRole(
                    AppRoles.SystemAdmin,
                    AppRoles.FactoryAdmin,
                    AppRoles.ProductionManager,
                    AppRoles.Viewer));

            options.AddPolicy(
                AuthorizationPolicies.CanViewQualityReports,
                policy => policy.RequireRole(
                    AppRoles.SystemAdmin,
                    AppRoles.FactoryAdmin,
                    AppRoles.QualityUser,
                    AppRoles.Viewer));
        });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = FactoryReportAuthDefaults.AntiforgeryHeaderName;
            options.Cookie.Name = FactoryReportAuthDefaults.AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }
}

/// <summary>
/// 基于 HttpContext.User 的当前用户访问实现。
/// </summary>
public sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUserAccessor
{
    public CurrentUser? GetCurrentUser()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = principal.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var displayName = principal.FindFirstValue("display_name") ?? userName;
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();

        return new CurrentUser
        {
            UserId = userId,
            UserName = userName,
            DisplayName = displayName,
            Roles = roles,
            IsAuthenticated = true
        };
    }
}
