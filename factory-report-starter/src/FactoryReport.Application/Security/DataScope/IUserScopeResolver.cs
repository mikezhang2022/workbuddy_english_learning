namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 解析当前用户服务端配置的数据范围。范围不得来自客户端 Claim / Header / query。
/// </summary>
public interface IUserScopeResolver
{
    /// <summary>
    /// 按用户 Id 解析数据范围。未知用户返回空范围（无授权）。
    /// </summary>
    Task<IDataScope> ResolveAsync(string userId, CancellationToken cancellationToken = default);
}
