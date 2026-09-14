using FactoryReport.Api.Endpoints;
using FactoryReport.Api.Health;
using FactoryReport.Api.Infrastructure;
using FactoryReport.Api.Logging;
using FactoryReport.Api.Middleware;
using FactoryReport.Api.Security;
using FactoryReport.Application.Abstractions;
using FactoryReport.Application.Configuration;
using FactoryReport.Infrastructure;
using FactoryReport.Infrastructure.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureOperationalLogging();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFactoryReportAuthentication(builder.Environment);

builder.Services.AddHealthChecks()
    .AddCheck(
        "live",
        () => HealthCheckResult.Healthy("Process is alive."),
        tags: ["live"])
    .AddCheck<FakeReadinessHealthCheck>(
        "ready",
        tags: ["ready"]);

var app = builder.Build();

// Production 不得静默启用 Fake 测试账号存储。
FakeAccountStoreProductionGuard.EnsureNotFakeInProduction(
    app.Environment,
    app.Services.GetRequiredService<IOptions<FactoryReportOptions>>().Value);

app.UseCorrelationId();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

// 认证能力（阶段 10）。现有报表 API 暂不强制 RequireAuthorization。
app.MapAuthEndpoints();

// 只读报表 API（阶段 5/6/7/8/9）：生产日报、工单进度、质量统计、计划达成、月度生产计划。无写入端点。
app.MapProductionDailyReportEndpoints();
app.MapWorkOrderProgressReportEndpoints();
app.MapQualityStatisticsReportEndpoints();
app.MapProductionPlanAchievementReportEndpoints();
app.MapMonthlyProductionPlanReportEndpoints();

app.MapGet("/health", (IDataAccessModeProvider modeProvider, IPlaceholderDataStore store) =>
{
    return Results.Json(new
    {
        status = "Healthy",
        mode = modeProvider.Mode.ToString(),
        isFake = modeProvider.IsFake,
        store = store.Describe(),
        utc = DateTime.UtcNow
    });
})
.WithName("Health");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponseAsync
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});

var options = app.Services.GetRequiredService<IOptions<FactoryReportOptions>>().Value;
if (options.ExposeTestExceptionEndpoint
    && (app.Environment.IsEnvironment("Testing") || app.Environment.IsDevelopment()))
{
    app.MapGet("/__test/exception", (HttpContext _) =>
    {
        throw new InvalidOperationException("Intentional test exception for ProblemDetails.");
    });
}

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description
        }),
        utc = DateTime.UtcNow
    };
    return context.Response.WriteAsJsonAsync(payload);
}

public partial class Program;
