using FactoryReport.Application.Security;

namespace FactoryReport.Worker.Security;

/// <summary>
/// Worker 无 HTTP 用户上下文；恒返回 null（匿名），仅满足 Infrastructure 依赖图解析。
/// </summary>
public sealed class AnonymousCurrentUserAccessor : ICurrentUserAccessor
{
    public CurrentUser? GetCurrentUser() => null;
}
