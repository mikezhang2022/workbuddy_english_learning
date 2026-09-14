using FactoryReport.Application.Security.DataScope;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.UnitTests.Security;

public class DataScopeIntersectionTests
{
    [Fact]
    public void Global_AllowsAnyFactory()
    {
        var filter = DataScopeIntersection.Intersect(
            UserDataScope.Global,
            DeterministicFakeFixture.FactoryDemo2Id,
            workshopId: null,
            productionLineId: null);

        Assert.Equal(DeterministicFakeFixture.FactoryDemo2Id, filter.FactoryId);
        Assert.Null(filter.WorkshopId);
    }

    [Fact]
    public void FactoryAdmin_DeniedOtherFactory()
    {
        var scope = UserDataScope.FromGrants(new DataScopeGrant(DeterministicFakeFixture.FactoryDemo1Id));
        var ex = Assert.Throws<DataScopeForbiddenException>(() =>
            DataScopeIntersection.Intersect(scope, DeterministicFakeFixture.FactoryDemo2Id, null, null));
        Assert.Contains("FactoryId", ex.Errors.Keys);
    }

    [Fact]
    public void ProductionManager_DeniedSiblingWorkshop()
    {
        var scope = UserDataScope.FromGrants(
            new DataScopeGrant(
                DeterministicFakeFixture.FactoryDemo1Id,
                DeterministicFakeFixture.WorkshopAId));

        var ex = Assert.Throws<DataScopeForbiddenException>(() =>
            DataScopeIntersection.Intersect(
                scope,
                DeterministicFakeFixture.FactoryDemo1Id,
                DeterministicFakeFixture.WorkshopBId,
                null));
        Assert.Contains("WorkshopId", ex.Errors.Keys);
    }

    [Fact]
    public void ProductionManager_OmittingWorkshop_NarrowsToAuthorizedWorkshop()
    {
        var scope = UserDataScope.FromGrants(
            new DataScopeGrant(
                DeterministicFakeFixture.FactoryDemo1Id,
                DeterministicFakeFixture.WorkshopAId));

        var filter = DataScopeIntersection.Intersect(
            scope,
            DeterministicFakeFixture.FactoryDemo1Id,
            null,
            null);

        Assert.Equal(DeterministicFakeFixture.WorkshopAId, filter.WorkshopId);
    }

    [Fact]
    public void QualityUser_DeniedOtherProductionLine()
    {
        var scope = UserDataScope.FromGrants(
            new DataScopeGrant(
                DeterministicFakeFixture.FactoryDemo1Id,
                DeterministicFakeFixture.WorkshopBId,
                DeterministicFakeFixture.LineB1Id));

        var ex = Assert.Throws<DataScopeForbiddenException>(() =>
            DataScopeIntersection.Intersect(
                scope,
                DeterministicFakeFixture.FactoryDemo1Id,
                DeterministicFakeFixture.WorkshopBId,
                DeterministicFakeFixture.LineA1Id));
        Assert.Contains("ProductionLineId", ex.Errors.Keys);
    }

    [Fact]
    public void QualityUser_OmittingFilters_NarrowsToLineGrant()
    {
        var scope = UserDataScope.FromGrants(
            new DataScopeGrant(
                DeterministicFakeFixture.FactoryDemo1Id,
                DeterministicFakeFixture.WorkshopBId,
                DeterministicFakeFixture.LineB1Id));

        var filter = DataScopeIntersection.Intersect(
            scope,
            DeterministicFakeFixture.FactoryDemo1Id,
            null,
            null);

        Assert.Equal(DeterministicFakeFixture.WorkshopBId, filter.WorkshopId);
        Assert.Equal(DeterministicFakeFixture.LineB1Id, filter.ProductionLineId);
    }

    [Fact]
    public void EmptyScope_DeniesEverything()
    {
        Assert.Throws<DataScopeForbiddenException>(() =>
            DataScopeIntersection.Intersect(
                UserDataScope.Empty,
                DeterministicFakeFixture.FactoryDemo1Id,
                null,
                null));
    }

    [Fact]
    public void DoesNotInheritSiblingWorkshopFromSameFactoryGrant()
    {
        var scope = UserDataScope.FromGrants(
            new DataScopeGrant(
                DeterministicFakeFixture.FactoryDemo1Id,
                DeterministicFakeFixture.WorkshopAId));

        Assert.True(scope.AllowsWorkshop(
            DeterministicFakeFixture.FactoryDemo1Id,
            DeterministicFakeFixture.WorkshopAId));
        Assert.False(scope.AllowsWorkshop(
            DeterministicFakeFixture.FactoryDemo1Id,
            DeterministicFakeFixture.WorkshopBId));
    }
}

public class FakeAccountDataScopeTests
{
    [Fact]
    public async Task FakeAccounts_HaveDeterministicServerSideScopes()
    {
        var store = new FakeLocalAccountStore(new FactoryReport.Infrastructure.Security.Pbkdf2PasswordHasher());

        var admin = await store.FindByUserIdAsync(FakeLocalAccountStore.UserId_SystemAdmin);
        Assert.NotNull(admin);
        Assert.True(admin!.DataScope.IsGlobal);

        var factoryAdmin = await store.FindByUserIdAsync(FakeLocalAccountStore.UserId_FactoryAdmin);
        Assert.False(factoryAdmin!.DataScope.IsGlobal);
        Assert.True(factoryAdmin.DataScope.AllowsFactory(DeterministicFakeFixture.FactoryDemo1Id));
        Assert.False(factoryAdmin.DataScope.AllowsFactory(DeterministicFakeFixture.FactoryDemo2Id));

        var viewer = await store.FindByUserIdAsync(FakeLocalAccountStore.UserId_Viewer);
        Assert.True(viewer!.DataScope.AllowsFactory(DeterministicFakeFixture.FactoryDemo2Id));
        Assert.False(viewer.DataScope.AllowsFactory(DeterministicFakeFixture.FactoryDemo1Id));
    }
}

public class ReportReadPolicyTests
{
    [Fact]
    public void ReportRead_PolicyName_IsStable()
    {
        Assert.Equal("ReportRead", FactoryReport.Domain.Security.AuthorizationPolicies.ReportRead);
    }
}
