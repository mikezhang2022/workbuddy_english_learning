using FactoryReport.Application.DataAccess;
using FactoryReport.Application.Security.DataScope;

namespace FactoryReport.UnitTests.Security;

/// <summary>
/// 单元测试用：不校验登录/授权，直接回传请求筛选（报表业务逻辑单测）。
/// </summary>
internal sealed class PermissiveReportQueryScopeService : IReportQueryScopeService
{
    public Task<OrganizationScopeFilter> ResolveEffectiveOrganizationScopeAsync(
        long factoryId,
        long? workshopId,
        long? productionLineId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new OrganizationScopeFilter(factoryId, workshopId, productionLineId));
}
