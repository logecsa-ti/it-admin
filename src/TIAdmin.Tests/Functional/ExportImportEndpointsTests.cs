namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Domain.Entities;
using Xunit;

public sealed class ExportImportEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task Export_Csv_ShouldContainHeadersRowsAndNeutralizeFormulas()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var tag = Tag();
        await CreateAssetAsync(admin, $"{tag}A", "Laptop normal");
        await CreateAssetAsync(admin, $"{tag}B", "=HYPERLINK(\"http://evil\")");

        var response = await admin.GetAsync("/api/v1/reports/assets/export?format=csv");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().Contain("assets-");
        body.Should().StartWith("Codigo,Serie,Nombre");
        body.Should().Contain($"{tag}A").And.Contain($"{tag}B");
        body.Should().Contain("'=HYPERLINK", "las formulas se neutralizan para evitar inyeccion al abrir en Excel");
    }

    [Fact]
    public async Task Export_Xlsx_ShouldBeAValidWorkbook()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var tag = Tag();
        await CreateAssetAsync(admin, $"{tag}X", "Monitor");

        var response = await admin.GetAsync($"/api/v1/reports/assets/export?format=xlsx");
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        sheet.Cell(1, 1).GetString().Should().Be("Codigo");
        sheet.Column(1).CellsUsed().Select(c => c.GetString()).Should().Contain($"{tag}X");
    }

    [Fact]
    public async Task Export_ShouldRequireExportAndModulePermissions()
    {
        using var admin = await factory.CreateAdminClientAsync();
        using var manager = await factory.CreateAssetManagerClientAsync();

        var managerExport = await manager.GetAsync("/api/v1/reports/assets/export?format=csv");
        var unknown = await admin.GetAsync("/api/v1/reports/planetas/export?format=csv");
        var pdf = await admin.GetAsync("/api/v1/reports/assets/export?format=pdf");
        var audit = await admin.GetAsync("/api/v1/reports/audit/export?format=csv&module=Organization");

        managerExport.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TI_ASSET_MANAGER no tiene REPORTS.EXPORT");
        (await ErrorCode(unknown, HttpStatusCode.BadRequest)).Should().Be("REPORT_NOT_FOUND");
        (await ErrorCode(pdf, HttpStatusCode.BadRequest)).Should().Be("EXPORT_FORMAT_NOT_SUPPORTED");
        audit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_AboveThreshold_ShouldRunAsJobAndNotifyWhenReady()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var other = await CreateUserAsync(SystemRoles.TiAdmin);
        await CreateAssetAsync(admin, $"{Tag()}1", "Uno");
        await CreateAssetAsync(admin, $"{Tag()}2", "Dos");
        (await admin.PutAsJsonAsync("/api/v1/configuration/Exports.AsyncThreshold", new { value = "1" })).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            var accepted = await admin.GetAsync("/api/v1/reports/assets/export?format=xlsx");
            accepted.StatusCode.Should().Be(HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync());
            var job = (await accepted.Content.ReadFromJsonAsync<ApiEnvelope<JobData>>(TestJson.Options))!.Data!;

            JobData? status = null;
            for (var attempt = 0; attempt < 50 && status?.Status is not ("Completed" or "Failed"); attempt++)
            {
                await Task.Delay(200);
                status = await Get<JobData>(admin, $"/api/v1/reports/exports/{job.Id}");
            }

            var download = await admin.GetAsync($"/api/v1/reports/exports/{job.Id}/download");
            var foreign = await other.Client.GetAsync($"/api/v1/reports/exports/{job.Id}");
            var inbox = await Get<PagedData<NotificationData>>(admin, "/api/v1/notifications?pageSize=200");

            status!.Status.Should().Be("Completed", status.Error);
            status.RowCount.Should().BeGreaterThanOrEqualTo(2);
            status.DownloadUrl.Should().Be($"/api/v1/reports/exports/{job.Id}/download");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            (await download.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();
            foreign.StatusCode.Should().Be(HttpStatusCode.NotFound, "solo quien la solicito ve la exportacion");
            inbox.Items.Should().Contain(n => n.Type == "ExportReady" && n.EntityId == job.Id);
        }
        finally
        {
            await admin.PostAsync("/api/v1/configuration/Exports.AsyncThreshold/reset", null);
        }
    }

    [Fact]
    public async Task Import_ShouldValidateEverythingAndBeAllOrNothing()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var tag = Tag();
        var location = await CreateLocationAsync();
        var valid = $"""
            AssetCode,Name,AssetType,SerialNumber,Brand,PurchaseDate,PurchaseCost,WarrantyExpiration,Location
            {tag}-01,Laptop importada,LAPTOP,SN-{tag}-1,Dell,2026-01-10,1200.50,2029-01-10,{location}
            {tag}-02,"Monitor, 27 pulgadas",MONITOR,,LG,,,,
            """;

        var dryRun = await ImportAsync(manager, valid, dryRun: true);
        var countAfterDryRun = await CountAssetsAsync(tag);
        var imported = await ImportAsync(manager, valid, dryRun: false);
        var countAfterImport = await CountAssetsAsync(tag);
        var stored = await factory.WithDbContextAsync(db => db.Assets.SingleAsync(a => a.AssetCode == $"{tag}-01"));

        dryRun.StatusCode.Should().Be(HttpStatusCode.OK);
        countAfterDryRun.Should().Be(0, "dryRun solo valida");
        imported.StatusCode.Should().Be(HttpStatusCode.OK, await imported.Content.ReadAsStringAsync());
        countAfterImport.Should().Be(2);
        stored.PurchaseCost.Should().Be(1200.50m);
        stored.LocationId.Should().NotBeNull();

        var invalid = $"""
            AssetCode,Name,AssetType,PurchaseDate,PurchaseCost
            {tag}-01,Duplicado en base,LAPTOP,,
            {tag}-03,Tipo inexistente,NAVE_ESPACIAL,,
            {tag}-04,Fecha mala,LAPTOP,10/01/2026,
            {tag}-04,Repetido en archivo,LAPTOP,,-5
            {tag}-05,Fila valida,LAPTOP,,
            """;

        var rejected = await ImportAsync(manager, invalid, dryRun: false);
        var errors = (await rejected.Content.ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Errors!.Select(e => e.Message).ToList();

        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        errors.Should().Contain(m => m.StartsWith("Fila 2, AssetCode") && m.Contains("ya existe"));
        errors.Should().Contain(m => m.StartsWith("Fila 3, AssetType"));
        errors.Should().Contain(m => m.StartsWith("Fila 4, PurchaseDate"));
        errors.Should().Contain(m => m.StartsWith("Fila 5, AssetCode") && m.Contains("repetido"));
        errors.Should().Contain(m => m.StartsWith("Fila 5, PurchaseCost"));
        (await CountAssetsAsync($"{tag}-05")).Should().Be(0, "con errores no se importa ninguna fila");
    }

    [Fact]
    public async Task Import_TemplateAndMissingColumns()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var endUser = await CreateUserAsync(SystemRoles.User);

        var template = await manager.GetAsync("/api/v1/assets/import/template?format=csv");
        var templateText = await template.Content.ReadAsStringAsync();
        var missing = await ImportAsync(manager, "Name,Brand\nAlgo,Dell", dryRun: true);
        var forbidden = await ImportAsync(endUser.Client, "AssetCode,Name,AssetType\nX,Y,LAPTOP", dryRun: true);

        template.StatusCode.Should().Be(HttpStatusCode.OK);
        templateText.Should().StartWith("AssetCode,Name,AssetType");
        (await ErrorCode(missing, HttpStatusCode.BadRequest)).Should().Be("IMPORT_MISSING_COLUMNS");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<int> CountAssetsAsync(string prefix) =>
        await factory.WithDbContextAsync(db => db.Assets.CountAsync(a => a.AssetCode.StartsWith(prefix)));

    private async Task<string> CreateLocationAsync()
    {
        var code = $"LOC{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
        await factory.WithDbContextAsync(async db =>
        {
            db.Locations.Add(new Location { Code = code, Name = $"Sede {code}" });
            return await db.SaveChangesAsync();
        });
        return code;
    }

    private static async Task<HttpResponseMessage> ImportAsync(HttpClient client, string csv, bool dryRun)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv.Replace("\r\n", "\n")));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "activos.csv");
        return await client.PostAsync($"/api/v1/assets/import?dryRun={dryRun}", form);
    }

    private static async Task CreateAssetAsync(HttpClient client, string code, string name) =>
        (await client.PostAsJsonAsync("/api/v1/assets", new { assetCode = code, name, assetTypeId = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

    private async Task<TestUser> CreateUserAsync(string role)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var userName = $"ex{Guid.NewGuid().ToString("N")[..8]}";
        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Ex", lastName = role
        });
        var id = (await response.Content.ReadFromJsonAsync<ApiEnvelope<IdData>>(TestJson.Options))!.Data!.Id;
        if (role != SystemRoles.User)
        {
            await admin.PutAsJsonAsync($"/api/v1/users/{id}/roles", new { roles = new[] { role } });
        }

        return new TestUser(id, await factory.LoginFreshAsync(userName, Password));
    }

    private static async Task<T> Get<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options))!.Data!;
    }

    private static async Task<string> ErrorCode(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Errors!.First().Code;
    }

    private static string Tag() => $"IMP{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();

    private sealed record TestUser(int Id, HttpClient Client);

    private sealed record IdData(int Id);

    private sealed record JobData(int Id, string Status, int? RowCount, string? Error, string? DownloadUrl);

    private sealed record NotificationData(int Id, string Type, int? EntityId);
}
