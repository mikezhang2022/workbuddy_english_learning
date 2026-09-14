using FactoryReport.Application.DataAccess;

namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 报表查询前的数据范围强制：解析当前用户服务端范围，与请求求交。
/// </summary>
public interface IReportQueryScopeService
{
    /// <summary>
    /// 校验并返回有效组织筛选。超出授权抛 <see cref="DataScopeForbiddenException"/>。
    /// </summary>
    Task<OrganizationScopeFilter> ResolveEffectiveOrganizationScopeAsync(
        long factoryId,
        long? workshopId,
        long? productionLineId,
        CancellationToken cancellationToken = default);
}
