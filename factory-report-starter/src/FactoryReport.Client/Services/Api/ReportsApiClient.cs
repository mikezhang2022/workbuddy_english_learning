using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FactoryReport.Application.Reporting.ProductionDaily;

namespace FactoryReport.Client.Services.Api;

/// <summary>
/// 报表只读 API 客户端。
/// </summary>
public sealed class ReportsApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string> GetRawAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(relativePath, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProductionDailyQueryResponse> GetProductionDailyAsync(
        ProductionDailyClientQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var path = BuildProductionDailyPath(query);
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
        var result = await response.Content
            .ReadFromJsonAsync<ProductionDailyQueryResponse>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new ApiRequestException(new ApiProblemDetails
        {
            Detail = "生产日报响应无效。"
        });
    }

    public static string BuildProductionDailyPath(ProductionDailyClientQuery query)
    {
        var sb = new StringBuilder("/api/v1/reports/production-daily?");
        sb.Append("factoryId=").Append(query.FactoryId.ToString(CultureInfo.InvariantCulture));
        sb.Append("&startDate=").Append(query.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        sb.Append("&endDate=").Append(query.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        if (query.WorkshopId is > 0)
        {
            sb.Append("&workshopId=").Append(query.WorkshopId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (query.ProductionLineId is > 0)
        {
            sb.Append("&productionLineId=")
                .Append(query.ProductionLineId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(query.ProductCode))
        {
            sb.Append("&productCode=").Append(Uri.EscapeDataString(query.ProductCode.Trim()));
        }

        return sb.ToString();
    }
}

/// <summary>
/// 生产日报客户端查询参数（与 API query 对齐）。
/// </summary>
public sealed class ProductionDailyClientQuery
{
    public long FactoryId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public long? WorkshopId { get; init; }
    public long? ProductionLineId { get; init; }
    public string? ProductCode { get; init; }
}
