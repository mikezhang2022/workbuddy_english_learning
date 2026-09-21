using FactoryReport.Api.Middleware;
using FactoryReport.Api.Security;
using FactoryReport.Application.Common;
using FactoryReport.Application.Import;
using FactoryReport.Domain.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace FactoryReport.Api.Endpoints;

/// <summary>
/// Excel 导入流水线端点：模板下载、上传、校验、列表/详情、发布、回退。
/// 写入操作需 Antiforgery；组织范围由服务层强制。
/// </summary>
public static class ExcelImportEndpoints
{
    public static IEndpointRouteBuilder MapExcelImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/imports").WithTags("Imports");

        group.MapGet("/templates", ListTemplatesAsync)
            .WithName("ListImportTemplates")
            .WithSummary("列出计划/实际 Excel 模板元数据")
            .RequireAuthorization(AuthorizationPolicies.ImportRead)
            .Produces<IReadOnlyList<ImportTemplateDefinition>>(StatusCodes.Status200OK);

        group.MapGet("/templates/{datasetCode}/download", DownloadTemplateAsync)
            .WithName("DownloadImportTemplate")
            .WithSummary("下载 xlsx 模板（含示例行与字段说明）")
            .RequireAuthorization(AuthorizationPolicies.ImportRead)
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/batches", ListBatchesAsync)
            .WithName("ListImportBatches")
            .WithSummary("导入批次列表")
            .RequireAuthorization(AuthorizationPolicies.ImportRead)
            .Produces<IReadOnlyList<ImportBatchSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/batches/{batchId:guid}", GetBatchAsync)
            .WithName("GetImportBatch")
            .WithSummary("导入批次详情（含行级错误）")
            .RequireAuthorization(AuthorizationPolicies.ImportRead)
            .Produces<ImportBatchDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/batches", UploadAsync)
            .WithName("UploadImportBatch")
            .WithSummary("上传 xlsx 并创建草稿批次（需 Antiforgery）")
            .DisableAntiforgery() // 手动校验，兼容 multipart
            .RequireAuthorization(AuthorizationPolicies.ImportManage)
            .Produces<ImportBatchDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/batches/{batchId:guid}/validate", ValidateAsync)
            .WithName("ValidateImportBatch")
            .WithSummary("校验批次（需 Antiforgery）")
            .RequireAuthorization(AuthorizationPolicies.ImportManage)
            .Produces<ImportValidationReportDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/batches/{batchId:guid}/publish", PublishAsync)
            .WithName("PublishImportBatch")
            .WithSummary("发布并激活数据集版本（需 Antiforgery；有错误不得发布）")
            .RequireAuthorization(AuthorizationPolicies.ImportManage)
            .Produces<ImportPublishResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/batches/{batchId:guid}/rollback", RollbackAsync)
            .WithName("RollbackImportBatch")
            .WithSummary("回退已发布版本（需 Antiforgery）")
            .RequireAuthorization(AuthorizationPolicies.ImportManage)
            .Produces<ImportRollbackResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static IResult ListTemplatesAsync(IExcelImportService importService)
        => Results.Ok(importService.ListTemplates());

    private static IResult DownloadTemplateAsync(string datasetCode, IExcelImportService importService)
    {
        try
        {
            var bytes = importService.DownloadTemplate(datasetCode);
            var fileName = $"{datasetCode}-template-v{ImportTemplateCatalog.TemplateVersion}.xlsx";
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (ReportQueryValidationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: ex.Title);
        }
    }

    private static async Task<IResult> ListBatchesAsync(
        [FromQuery] long? factoryId,
        [FromQuery] string? datasetCode,
        IExcelImportService importService,
        CancellationToken cancellationToken)
    {
        if (factoryId is null or <= 0)
        {
            return Results.Problem(
                detail: "factoryId is required and must be positive.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more validation errors occurred.");
        }

        var list = await importService
            .ListBatchesAsync(factoryId.Value, datasetCode, cancellationToken)
            .ConfigureAwait(false);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetBatchAsync(
        Guid batchId,
        IExcelImportService importService,
        CancellationToken cancellationToken)
    {
        var detail = await importService.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(detail);
    }

    private static async Task<IResult> UploadAsync(
        HttpContext httpContext,
        IExcelImportService importService,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("FactoryReport.Import");
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(
                ex,
                "Import upload rejected: antiforgery failed. CorrelationId={CorrelationId}",
                httpContext.GetCorrelationId());
            return Results.Problem(
                detail: "Antiforgery token validation failed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        if (!httpContext.Request.HasFormContentType)
        {
            return Results.Problem(
                detail: "Content-Type must be multipart/form-data.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        if (!long.TryParse(form["factoryId"], out var factoryId) || factoryId <= 0)
        {
            return Results.Problem(
                detail: "factoryId is required and must be positive.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more validation errors occurred.");
        }

        var datasetCode = form["datasetCode"].ToString();
        if (string.IsNullOrWhiteSpace(datasetCode))
        {
            return Results.Problem(
                detail: "datasetCode is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more validation errors occurred.");
        }

        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length <= 0)
        {
            return Results.Problem(
                detail: "file is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more validation errors occurred.");
        }

        var contentType = file.ContentType ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(contentType)
            && !ExcelImportOptions.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            && !contentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase)
            && !contentType.Contains("excel", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                detail: $"不支持的 Content-Type：{contentType}。请上传 .xlsx。",
                statusCode: StatusCodes.Status400BadRequest,
                title: "One or more validation errors occurred.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var detail = await importService
                .UploadAsync(factoryId, datasetCode, file.FileName, stream, file.Length, cancellationToken)
                .ConfigureAwait(false);
            return Results.Created($"/api/v1/imports/batches/{detail.BatchId}", detail);
        }
        catch (ExcelParseException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Excel parse failed");
        }
        catch (ReportQueryValidationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: ex.Title);
        }
    }

    private static async Task<IResult> ValidateAsync(
        Guid batchId,
        HttpContext httpContext,
        IExcelImportService importService,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var antiforgeryResult = await ValidateAntiforgeryAsync(httpContext, antiforgery, loggerFactory)
            .ConfigureAwait(false);
        if (antiforgeryResult is not null)
        {
            return antiforgeryResult;
        }

        try
        {
            var report = await importService.ValidateAsync(batchId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(report);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid state");
        }
    }

    private static async Task<IResult> PublishAsync(
        Guid batchId,
        HttpContext httpContext,
        IExcelImportService importService,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var antiforgeryResult = await ValidateAntiforgeryAsync(httpContext, antiforgery, loggerFactory)
            .ConfigureAwait(false);
        if (antiforgeryResult is not null)
        {
            return antiforgeryResult;
        }

        try
        {
            var result = await importService.PublishAsync(batchId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid state");
        }
    }

    private static async Task<IResult> RollbackAsync(
        Guid batchId,
        HttpContext httpContext,
        IExcelImportService importService,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var antiforgeryResult = await ValidateAntiforgeryAsync(httpContext, antiforgery, loggerFactory)
            .ConfigureAwait(false);
        if (antiforgeryResult is not null)
        {
            return antiforgeryResult;
        }

        try
        {
            var result = await importService.RollbackAsync(batchId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid state");
        }
    }

    private static async Task<IResult?> ValidateAntiforgeryAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        ILoggerFactory loggerFactory)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext).ConfigureAwait(false);
            return null;
        }
        catch (AntiforgeryValidationException ex)
        {
            loggerFactory.CreateLogger("FactoryReport.Import").LogWarning(
                ex,
                "Import mutation rejected: antiforgery failed. CorrelationId={CorrelationId}",
                httpContext.GetCorrelationId());
            return Results.Problem(
                detail: "Antiforgery token validation failed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }
    }
}
