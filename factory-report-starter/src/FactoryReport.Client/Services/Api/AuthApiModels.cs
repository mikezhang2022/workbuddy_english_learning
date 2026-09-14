using FactoryReport.Application.Security.DataScope;

namespace FactoryReport.Client.Services.Api;

public sealed class AuthLoginRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

public sealed class AuthMeDto
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public bool IsAuthenticated { get; init; }
    public DataScopeSummary? DataScope { get; init; }
}
