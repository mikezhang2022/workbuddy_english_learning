using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryReport.Application.Import;

namespace FactoryReport.Client.Services.Api;

public sealed class ImportApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<ImportTemplateDefinition>> ListTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("/api/v1/imports/templates", cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        var list = await response.Content
            .ReadFromJsonAsync<List<ImportTemplateDefinition>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<byte[]> DownloadTemplateAsync(string datasetCode, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client
                .GetAsync($"/api/v1/imports/templates/{Uri.EscapeDataString(datasetCode)}/download", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ImportBatchSummaryDto>> ListBatchesAsync(
        long factoryId,
        string? datasetCode = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/v1/imports/batches?factoryId={factoryId}";
        if (!string.IsNullOrWhiteSpace(datasetCode))
        {
            path += $"&datasetCode={Uri.EscapeDataString(datasetCode)}";
        }

        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        var list = await response.Content
            .ReadFromJsonAsync<List<ImportBatchSummaryDto>>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<ImportBatchDetailDto> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"/api/v1/imports/batches/{batchId}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ImportBatchDetailDto>(JsonOptions, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new ApiRequestException(new ApiProblemDetails { Detail = "批次详情响应无效。" });
    }

    public async Task<ImportBatchDetailDto> UploadAsync(
        long factoryId,
        string datasetCode,
        string fileName,
        Stream content,
        string csrfToken,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(factoryId.ToString()), "factoryId");
        form.Add(new StringContent(datasetCode), "datasetCode");
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/imports/batches")
        {
            Content = form
        };
        ApiResponseHandler.ApplyAntiforgeryToken(request, csrfToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ImportBatchDetailDto>(JsonOptions, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new ApiRequestException(new ApiProblemDetails { Detail = "上传响应无效。" });
    }

    public async Task<ImportValidationReportDto> ValidateAsync(
        Guid batchId,
        string csrfToken,
        CancellationToken cancellationToken = default)
        => await PostJsonAsync<ImportValidationReportDto>(
                $"/api/v1/imports/batches/{batchId}/validate",
                csrfToken,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<ImportPublishResultDto> PublishAsync(
        Guid batchId,
        string csrfToken,
        CancellationToken cancellationToken = default)
        => await PostJsonAsync<ImportPublishResultDto>(
                $"/api/v1/imports/batches/{batchId}/publish",
                csrfToken,
                cancellationToken)
            .ConfigureAwait(false);

    public async Task<ImportRollbackResultDto> RollbackAsync(
        Guid batchId,
        string csrfToken,
        CancellationToken cancellationToken = default)
        => await PostJsonAsync<ImportRollbackResultDto>(
                $"/api/v1/imports/batches/{batchId}/rollback",
                csrfToken,
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<T> PostJsonAsync<T>(
        string path,
        string csrfToken,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        ApiResponseHandler.ApplyAntiforgeryToken(request, csrfToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false)
               ?? throw new ApiRequestException(new ApiProblemDetails { Detail = "导入操作响应无效。" });
    }
}
