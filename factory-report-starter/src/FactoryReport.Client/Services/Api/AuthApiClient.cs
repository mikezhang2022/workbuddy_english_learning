using System.Net.Http.Json;
using System.Text.Json;

namespace FactoryReport.Client.Services.Api;

public sealed class AuthApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<(AuthMeDto User, string? CsrfToken)> LoginAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new AuthLoginRequest { UserName = userName, Password = password })
        };
        ApiResponseHandler.ApplyCorrelationId(request, Guid.NewGuid().ToString("N"));

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

        var user = await response.Content.ReadFromJsonAsync<AuthMeDto>(JsonOptions, cancellationToken)
                   .ConfigureAwait(false)
                   ?? throw new ApiRequestException(new ApiProblemDetails { Detail = "登录响应无效。" });

        var csrf = ApiResponseHandler.ReadAntiforgeryToken(response);
        return (user, csrf);
    }

    public async Task<AuthMeDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("/api/v1/auth/me", cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<AuthMeDto>(JsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> RefreshCsrfTokenAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("/api/v1/auth/csrf", cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new ApiRequestException(ApiProblemDetailsParser.NetworkError());
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        await ApiResponseHandler.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return ApiResponseHandler.ReadAntiforgeryToken(response);
    }

    public async Task LogoutAsync(string csrfToken, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(ApiConstants.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
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
    }
}
