namespace FactoryReport.Application.Security;

/// <summary>
/// 当前用户访问抽象。由 API 宿主基于 Cookie 主体实现；Application 不依赖 HttpContext。
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// 若未登录返回 null。
    /// </summary>
    CurrentUser? GetCurrentUser();
}
