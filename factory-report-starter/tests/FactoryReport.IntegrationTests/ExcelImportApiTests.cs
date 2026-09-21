using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FactoryReport.Api.Security;
using FactoryReport.Application.Import;
using FactoryReport.Domain.Import;
using FactoryReport.Infrastructure.Excel;
using FactoryReport.Infrastructure.Fake;

namespace FactoryReport.IntegrationTests;

public class ExcelImportApiTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public ExcelImportApiTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthenticated_Upload_Returns401()
    {
        var client = AuthenticatedClientFactory.CreateCookieClient(_factory);
        using var content = BuildUploadContent(ImportDatasetCodes.PlanData, BuildValidPlanBytes());
        var response = await client.PostAsync("/api/v1/imports/batches", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_Upload_Returns403()
    {
        var client = await AuthenticatedClientFactory.CreateViewerClientAsync(_factory);
        using var content = BuildUploadContent(ImportDatasetCodes.PlanData, BuildValidPlanBytes());
        // Viewer lacks ImportManage even with CSRF
        var csrf = await EnsureCsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/imports/batches")
        {
            Content = BuildUploadContent(ImportDatasetCodes.PlanData, BuildValidPlanBytes())
        };
        request.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FactoryAdmin_CannotAccess_OtherFactory()
    {
        var client = await AuthenticatedClientFactory.CreateFactoryAdminClientAsync(_factory);
        var list = await client.GetAsync("/api/v1/imports/batches?factoryId=2");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Upload_Validate_Publish_Rollback_Flow()
    {
        var client = await AuthenticatedClientFactory.CreateSystemAdminClientAsync(_factory);
        var csrf = await EnsureCsrfAsync(client);

        using (var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/imports/batches")
        {
            Content = BuildUploadContent(ImportDatasetCodes.PlanData, BuildValidPlanBytes())
        })
        {
            uploadRequest.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
            var upload = await client.SendAsync(uploadRequest);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            using var uploadDoc = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            var batchId = uploadDoc.RootElement.GetProperty("batchId").GetGuid();
            Assert.Equal("Pending", uploadDoc.RootElement.GetProperty("status").GetString());

            csrf = await EnsureCsrfAsync(client);
            using var validateRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/imports/batches/{batchId}/validate");
            validateRequest.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
            var validate = await client.SendAsync(validateRequest);
            Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
            using var validateDoc = JsonDocument.Parse(await validate.Content.ReadAsStringAsync());
            Assert.Equal(0, validateDoc.RootElement.GetProperty("errorRows").GetInt32());
            Assert.True(validateDoc.RootElement.GetProperty("canPublish").GetBoolean());

            csrf = await EnsureCsrfAsync(client);
            using var publishRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/imports/batches/{batchId}/publish");
            publishRequest.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
            var publish = await client.SendAsync(publishRequest);
            Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
            using var publishDoc = JsonDocument.Parse(await publish.Content.ReadAsStringAsync());
            Assert.True(publishDoc.RootElement.GetProperty("isActive").GetBoolean());

            csrf = await EnsureCsrfAsync(client);
            using var rollbackRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/imports/batches/{batchId}/rollback");
            rollbackRequest.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
            var rollback = await client.SendAsync(rollbackRequest);
            Assert.Equal(HttpStatusCode.OK, rollback.StatusCode);
        }
    }

    [Fact]
    public async Task Validate_WithErrors_CannotPublish()
    {
        var client = await AuthenticatedClientFactory.CreateSystemAdminClientAsync(_factory);
        var csrf = await EnsureCsrfAsync(client);
        var badBytes = BuildPlanBytes(new Dictionary<string, object?>
        {
            ["factoryCode"] = "F-DEMO-01",
            ["workshopCode"] = "W-DEMO-A",
            ["productionLineCode"] = "L-A1",
            ["planYearMonth"] = "2026-03",
            ["productionDate"] = "2026-03-15",
            ["productCode"] = "PROD-NOT-EXIST",
            ["planQuantity"] = -1,
            ["remark"] = "bad"
        });

        Guid batchId;
        using (var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/imports/batches")
        {
            Content = BuildUploadContent(ImportDatasetCodes.PlanData, badBytes)
        })
        {
            uploadRequest.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
            var upload = await client.SendAsync(uploadRequest);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            using var uploadDoc = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            batchId = uploadDoc.RootElement.GetProperty("batchId").GetGuid();
        }

        csrf = await EnsureCsrfAsync(client);
        using var validateRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/imports/batches/{batchId}/validate");
        validateRequest.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
        var validate = await client.SendAsync(validateRequest);
        Assert.Equal(HttpStatusCode.OK, validate.StatusCode);
        using var validateDoc = JsonDocument.Parse(await validate.Content.ReadAsStringAsync());
        Assert.True(validateDoc.RootElement.GetProperty("errorRows").GetInt32() > 0);
        Assert.False(validateDoc.RootElement.GetProperty("canPublish").GetBoolean());
        var errors = validateDoc.RootElement.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.True(errors[0].TryGetProperty("rowNumber", out _));
        Assert.True(errors[0].TryGetProperty("columnName", out _));
        Assert.True(errors[0].TryGetProperty("reason", out _));

        csrf = await EnsureCsrfAsync(client);
        using var publishRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/imports/batches/{batchId}/publish");
        publishRequest.Headers.TryAddWithoutValidation(FactoryReportAuthDefaults.AntiforgeryHeaderName, csrf);
        var publish = await client.SendAsync(publishRequest);
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
    }

    [Fact]
    public async Task TemplateDownload_ReturnsXlsx()
    {
        var client = await AuthenticatedClientFactory.CreateSystemAdminClientAsync(_factory);
        var response = await client.GetAsync("/api/v1/imports/templates/monthly_production_plan/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }

    private static async Task<string> EnsureCsrfAsync(HttpClient client)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        csrfResponse.EnsureSuccessStatusCode();
        return csrfResponse.Headers.GetValues(FactoryReportAuthDefaults.AntiforgeryHeaderName).Single();
    }

    private static MultipartFormDataContent BuildUploadContent(string datasetCode, byte[] bytes)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent("1"), "factoryId");
        form.Add(new StringContent(datasetCode), "datasetCode");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "plan.xlsx");
        return form;
    }

    private static byte[] BuildValidPlanBytes()
        => BuildPlanBytes(new Dictionary<string, object?>
        {
            ["factoryCode"] = "F-DEMO-01",
            ["workshopCode"] = "W-DEMO-A",
            ["productionLineCode"] = "L-A1",
            ["planYearMonth"] = "2026-03",
            ["productionDate"] = "2026-03-15",
            ["productCode"] = "PROD-NORMAL",
            ["planQuantity"] = 42,
            ["remark"] = "it-flow"
        });

    private static byte[] BuildPlanBytes(Dictionary<string, object?> row)
    {
        var template = ImportTemplateCatalog.Plan;
        var service = new MiniExcelWorkbookService();
        // Generate template then re-parse is heavy; build via CreateTemplate sample override
        var def = new ImportTemplateDefinition
        {
            DatasetCode = template.DatasetCode,
            DisplayName = template.DisplayName,
            TemplateVersion = template.TemplateVersion,
            SheetName = template.SheetName,
            Columns = template.Columns,
            SampleRows = [row],
            FieldNotes = template.FieldNotes
        };
        return service.CreateTemplate(def);
    }
}
