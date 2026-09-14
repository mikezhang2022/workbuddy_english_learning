namespace FactoryReport.Client.Services.Api;

/// <summary>
/// 报表只读 API 客户端基础（阶段 12 仅封装通用 GET；具体报表页后续接入）。
/// </summary>
public sealed class ReportsApiClient(IHttpClientFactory httpClientFactory)
{
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
}
