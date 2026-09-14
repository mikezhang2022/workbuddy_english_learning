using FactoryReport.Application.DataAccess;

namespace FactoryReport.Application.Security.DataScope;

/// <summary>
/// 使用 <see cref="ICurrentUserAccessor"/> + <see cref="IUserScopeResolver"/> 强制报表查询数据范围。
/// 范围始终按服务端 UserId 解析，忽略客户端伪造的角色 Claim。
/// 工厂级授权时额外校验车间/产线是否归属该工厂（防伪造异厂组织 Id）。
/// </summary>
public sealed class ReportQueryScopeService(
    ICurrentUserAccessor currentUserAccessor,
    IUserScopeResolver userScopeResolver,
    IOrganizationReadRepository organizationReadRepository) : IReportQueryScopeService
{
    public async Task<OrganizationScopeFilter> ResolveEffectiveOrganizationScopeAsync(
        long factoryId,
        long? workshopId,
        long? productionLineId,
        CancellationToken cancellationToken = default)
    {
        var user = currentUserAccessor.GetCurrentUser();
        if (user is null || !user.IsAuthenticated)
        {
            throw DataScopeForbiddenException.NotAuthenticated();
        }

        var scope = await userScopeResolver
            .ResolveAsync(user.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (workshopId is not null)
        {
            await EnsureWorkshopBelongsToFactoryAsync(factoryId, workshopId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        if (productionLineId is not null)
        {
            var resolvedWorkshopId = await EnsureProductionLineBelongsToFactoryAsync(
                    factoryId,
                    workshopId,
                    productionLineId.Value,
                    cancellationToken)
                .ConfigureAwait(false);

            workshopId ??= resolvedWorkshopId;
        }

        return DataScopeIntersection.Intersect(scope, factoryId, workshopId, productionLineId);
    }

    private async Task EnsureWorkshopBelongsToFactoryAsync(
        long factoryId,
        long workshopId,
        CancellationToken cancellationToken)
    {
        var workshops = await organizationReadRepository
            .GetWorkshopsAsync(factoryId, cancellationToken)
            .ConfigureAwait(false);

        if (workshops.All(w => w.Id != workshopId))
        {
            throw DataScopeForbiddenException.ForWorkshop(factoryId, workshopId);
        }
    }

    /// <returns>产线所属车间 Id。</returns>
    private async Task<long> EnsureProductionLineBelongsToFactoryAsync(
        long factoryId,
        long? workshopId,
        long productionLineId,
        CancellationToken cancellationToken)
    {
        var lines = await organizationReadRepository
            .GetProductionLinesAsync(factoryId, workshopId: null, cancellationToken)
            .ConfigureAwait(false);

        var line = lines.FirstOrDefault(l => l.Id == productionLineId);
        if (line is null || line.FactoryId != factoryId)
        {
            throw DataScopeForbiddenException.ForProductionLine(factoryId, workshopId, productionLineId);
        }

        if (workshopId is not null && line.WorkshopId != workshopId.Value)
        {
            throw DataScopeForbiddenException.ForProductionLine(factoryId, workshopId, productionLineId);
        }

        return line.WorkshopId;
    }
}
