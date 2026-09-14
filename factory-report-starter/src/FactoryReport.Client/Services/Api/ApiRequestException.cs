namespace FactoryReport.Client.Services.Api;

public sealed class ApiRequestException : Exception
{
    public ApiRequestException(ApiProblemDetails problem)
        : base(problem.UserFacingMessage)
    {
        Problem = problem;
    }

    public ApiProblemDetails Problem { get; }

    public bool IsUnauthorized => Problem.Status == 401;
    public bool IsForbidden => Problem.Status == 403;
}
