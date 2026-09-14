using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryReport.Api.Endpoints;
using FactoryReport.Api.Security;
using FactoryReport.Domain.Security;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.IntegrationTests;

public class ReportAuthorizationApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    private static readonly string[] ReportUrls =
    [
        "/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10",
        "/api/v1/reports/work-order-progress?factoryId=1",
        "/api/v1/reports/quality-statistics?factoryId=1&startDate=2026-03-10&endDate=2026-03-10",
        "/api/v1/reports/production-plan-achievement?factoryId=1&startDate=2026-03-10&endDate=2026-03-10",
        "/api/v1/reports/monthly-production-plan?factoryId=1&planMonth=2026-03"
    ];

    public ReportAuthorizationApiTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10")]
    [InlineData("/api/v1/reports/work-order-progress?factoryId=1")]
    [InlineData("/api/v1/reports/quality-statistics?factoryId=1&startDate=2026-03-10&endDate=2026-03-10")]
    [InlineData("/api/v1/reports/production-plan-achievement?factoryId=1&startDate=2026-03-10&endDate=2026-03-10")]
    [InlineData("/api/v1/reports/monthly-production-plan?factoryId=1&planMonth=2026-03")]
    public async Task Anonymous_ReportAccess_Returns401(string url)
    {
        var client = AuthenticatedClientFactory.CreateCookieClient(_factory);
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_And_Login_RemainAnonymous()
    {
        var client = AuthenticatedClientFactory.CreateCookieClient(_factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = "viewer",
            Password = FakeLocalAccountStore.DevPassword_Viewer
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task SystemAdmin_CanAccessBothFactories()
    {
        var client = await AuthenticatedClientFactory.CreateSystemAdminClientAsync(_factory);

        var f1 = await client.GetAsync(
            "/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10");
        var f2 = await client.GetAsync(
            "/api/v1/reports/production-daily?factoryId=2&startDate=2026-03-10&endDate=2026-03-10");

        Assert.Equal(HttpStatusCode.OK, f1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, f2.StatusCode);
    }

    [Fact]
    public async Task FactoryAdmin_OnlyAuthorizedFactory()
    {
        var client = await AuthenticatedClientFactory.CreateFactoryAdminClientAsync(_factory);

        var ok = await client.GetAsync(
            "/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10");
        var denied = await client.GetAsync(
            "/api/v1/reports/production-daily?factoryId=2&startDate=2026-03-10&endDate=2026-03-10");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await AssertProblemStatus(denied, 403);
    }

    [Fact]
    public async Task ProductionManager_CannotEscalateViaWorkshopOrLineQuery()
    {
        var client = await AuthenticatedClientFactory.CreateProductionManagerClientAsync(_factory);

        var allowed = await client.GetAsync(
            $"/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10&workshopId={DeterministicFakeFixture.WorkshopAId}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        var siblingWorkshop = await client.GetAsync(
            $"/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10&workshopId={DeterministicFakeFixture.WorkshopBId}");
        Assert.Equal(HttpStatusCode.Forbidden, siblingWorkshop.StatusCode);

        var otherFactory = await client.GetAsync(
            "/api/v1/reports/work-order-progress?factoryId=2");
        Assert.Equal(HttpStatusCode.Forbidden, otherFactory.StatusCode);

        var foreignLine = await client.GetAsync(
            $"/api/v1/reports/quality-statistics?factoryId=1&startDate=2026-03-10&endDate=2026-03-10&productionLineId={DeterministicFakeFixture.LineB1Id}");
        Assert.Equal(HttpStatusCode.Forbidden, foreignLine.StatusCode);
    }

    [Fact]
    public async Task QualityUser_LineScope_ForbidsOutOfScopeAndAllowsInScope()
    {
        var client = await AuthenticatedClientFactory.CreateQualityUserClientAsync(_factory);

        var ok = await client.GetAsync(
            $"/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10&workshopId={DeterministicFakeFixture.WorkshopBId}&productionLineId={DeterministicFakeFixture.LineB1Id}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var badLine = await client.GetAsync(
            $"/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10&workshopId={DeterministicFakeFixture.WorkshopBId}&productionLineId={DeterministicFakeFixture.LineA1Id}");
        Assert.Equal(HttpStatusCode.Forbidden, badLine.StatusCode);

        var badWorkshop = await client.GetAsync(
            $"/api/v1/reports/monthly-production-plan?factoryId=1&planMonth=2026-03&workshopId={DeterministicFakeFixture.WorkshopAId}");
        Assert.Equal(HttpStatusCode.Forbidden, badWorkshop.StatusCode);
    }

    [Fact]
    public async Task Viewer_OnlyFactory2_WithinScopeOk_OutsideForbidden()
    {
        var client = await AuthenticatedClientFactory.CreateViewerClientAsync(_factory);

        var ok = await client.GetAsync(
            "/api/v1/reports/production-plan-achievement?factoryId=2&startDate=2026-03-10&endDate=2026-03-10");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var denied = await client.GetAsync(
            "/api/v1/reports/production-plan-achievement?factoryId=1&startDate=2026-03-10&endDate=2026-03-10");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task ForgedRoleHeader_DoesNotExpandScope()
    {
        var client = await AuthenticatedClientFactory.CreateViewerClientAsync(_factory);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/reports/production-daily?factoryId=1&startDate=2026-03-10&endDate=2026-03-10");
        request.Headers.TryAddWithoutValidation("X-Role", AppRoles.SystemAdmin);
        request.Headers.TryAddWithoutValidation("Role", AppRoles.SystemAdmin);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsDataScopeSummary_FromServerConfig()
    {
        var client = await AuthenticatedClientFactory.CreateFactoryAdminClientAsync(_factory);
        var me = await client.GetFromJsonAsync<AuthMeResponse>("/api/v1/auth/me");
        Assert.NotNull(me);
        Assert.Equal(new[] { AppRoles.FactoryAdmin }, me!.Roles.ToArray());
        Assert.NotNull(me.DataScope);
        Assert.False(me.DataScope!.IsGlobal);
        Assert.Contains(me.DataScope.Grants, g => g.FactoryId == DeterministicFakeFixture.FactoryDemo1Id);
        Assert.DoesNotContain(me.DataScope.Grants, g => g.FactoryId == DeterministicFakeFixture.FactoryDemo2Id);
    }

    [Fact]
    public async Task AllFiveRoles_CanReadReports_WithinTheirScope()
    {
        foreach (var url in ReportUrls)
        {
            // SystemAdmin covers factory 1 URLs.
            var admin = await AuthenticatedClientFactory.CreateSystemAdminClientAsync(_factory);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        }

        var factoryAdmin = await AuthenticatedClientFactory.CreateFactoryAdminClientAsync(_factory);
        Assert.Equal(
            HttpStatusCode.OK,
            (await factoryAdmin.GetAsync(ReportUrls[0])).StatusCode);

        var prod = await AuthenticatedClientFactory.CreateProductionManagerClientAsync(_factory);
        Assert.Equal(
            HttpStatusCode.OK,
            (await prod.GetAsync(ReportUrls[0])).StatusCode);

        var quality = await AuthenticatedClientFactory.CreateQualityUserClientAsync(_factory);
        Assert.Equal(
            HttpStatusCode.OK,
            (await quality.GetAsync(
                $"/api/v1/reports/quality-statistics?factoryId=1&startDate=2026-03-10&endDate=2026-03-10&workshopId={DeterministicFakeFixture.WorkshopBId}&productionLineId={DeterministicFakeFixture.LineB1Id}")).StatusCode);

        var viewer = await AuthenticatedClientFactory.CreateViewerClientAsync(_factory);
        Assert.Equal(
            HttpStatusCode.OK,
            (await viewer.GetAsync(
                "/api/v1/reports/monthly-production-plan?factoryId=2&planMonth=2026-03")).StatusCode);
    }

    [Fact]
    public async Task CookieAuth_StillWorks_WithAntiforgeryLogout()
    {
        var client = AuthenticatedClientFactory.CreateCookieClient(_factory);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            UserName = "sysadmin",
            Password = FakeLocalAccountStore.DevPassword_SystemAdmin
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var csrf = login.Headers.GetValues(FactoryReportAuthDefaults.AntiforgeryHeaderName).Single();

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.TryAddWithoutValidation(
            FactoryReportAuthDefaults.AntiforgeryHeaderName,
            csrf);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logoutRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ReportUrls[0])).StatusCode);
    }

    private static async Task AssertProblemStatus(HttpResponseMessage response, int status)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status, doc.RootElement.GetProperty("status").GetInt32());
    }
}
