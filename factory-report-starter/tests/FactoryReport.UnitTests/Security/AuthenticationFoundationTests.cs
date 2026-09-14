using FactoryReport.Application.Security;
using FactoryReport.Domain.Security;
using FactoryReport.Infrastructure.Fake;
using FactoryReport.Infrastructure.Security;

namespace FactoryReport.UnitTests.Security;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ThenVerify_Succeeds_AndRejectsWrongPassword()
    {
        var hash = _hasher.HashPassword("Dev-Only-Sample-Passw0rd!");
        Assert.StartsWith("PBKDF2-SHA256$", hash);
        Assert.True(_hasher.VerifyHashedPassword(hash, "Dev-Only-Sample-Passw0rd!"));
        Assert.False(_hasher.VerifyHashedPassword(hash, "wrong"));
        Assert.DoesNotContain("Dev-Only-Sample-Passw0rd!", hash, StringComparison.Ordinal);
    }
}

public class LocalAccountAuthenticationServiceTests
{
    private readonly LocalAccountAuthenticationService _service;

    public LocalAccountAuthenticationServiceTests()
    {
        var hasher = new Pbkdf2PasswordHasher();
        _service = new LocalAccountAuthenticationService(new FakeLocalAccountStore(hasher), hasher);
    }

    [Fact]
    public async Task Authenticate_ValidViewer_BuildsRoleClaims()
    {
        var result = await _service.AuthenticateAsync("viewer", FakeLocalAccountStore.DevPassword_Viewer);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Contains(AppRoles.Viewer, result.User!.Roles);
        Assert.Contains(result.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == AppRoles.Viewer);
        Assert.DoesNotContain(result.Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missing", "x")]
    [InlineData("viewer", "bad")]
    public async Task Authenticate_Invalid_ReturnsUniformFailure(string user, string password)
    {
        var a = await _service.AuthenticateAsync(user, password);
        var b = await _service.AuthenticateAsync("also-missing", "also-bad");
        Assert.False(a.Succeeded);
        Assert.False(b.Succeeded);
        Assert.Null(a.User);
        Assert.Null(b.User);
    }
}

public class FakeLocalAccountStoreTests
{
    [Fact]
    public async Task Store_IsFake_AndAccountsAreTestOnly()
    {
        var store = new FakeLocalAccountStore(new Pbkdf2PasswordHasher());
        Assert.True(store.IsFake);
        Assert.Equal(FakeLocalAccountStore.StoreKindName, store.StoreKind);

        var admin = await store.FindByUserNameAsync("sysadmin");
        Assert.NotNull(admin);
        Assert.True(admin!.IsTestOnlyAccount);
        Assert.DoesNotContain(FakeLocalAccountStore.DevPassword_SystemAdmin, admin.PasswordHash);
    }
}

public class AppRolesAndPoliciesTests
{
    [Fact]
    public void Phase10_Roles_AreExactlyFive()
    {
        Assert.Equal(5, AppRoles.All.Count);
        Assert.All(
            new[]
            {
                AppRoles.SystemAdmin,
                AppRoles.FactoryAdmin,
                AppRoles.ProductionManager,
                AppRoles.QualityUser,
                AppRoles.Viewer
            },
            role => Assert.True(AppRoles.IsKnown(role)));
    }

    [Fact]
    public void AuthorizationPolicyNames_AreStable()
    {
        Assert.Equal("RequireAuthenticated", AuthorizationPolicies.RequireAuthenticated);
        Assert.Equal("CanViewProductionReports", AuthorizationPolicies.CanViewProductionReports);
        Assert.Equal("CanViewQualityReports", AuthorizationPolicies.CanViewQualityReports);
    }
}
