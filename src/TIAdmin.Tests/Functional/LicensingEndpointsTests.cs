namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Domain.Enums;
using Xunit;

public sealed class LicensingEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";
    private const string PlainKey = "ABCDE-12345-FGHIJ-67890";
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Software_ShouldEnforceUniqueNameVersionAndBlockDeleteWithLicenses()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var name = Unique("Office");

        var v1 = await Post<SoftwareData>(admin, "/api/v1/software", NewSoftware(name, "2021"));
        var v2 = await admin.PostAsJsonAsync("/api/v1/software", NewSoftware(name, "2024"));
        var duplicate = await admin.PostAsJsonAsync("/api/v1/software", NewSoftware(name, "2021"));
        await Post<LicenseData>(admin, "/api/v1/licenses", NewLicense(v1.Id, quantity: 5));
        var delete = await admin.DeleteAsync($"/api/v1/software/{v1.Id}");
        var withSeats = await Get<SoftwareData>(admin, $"/api/v1/software/{v1.Id}");

        v2.StatusCode.Should().Be(HttpStatusCode.Created, "misma marca, otra version");
        (await ErrorCode(duplicate, HttpStatusCode.Conflict)).Should().Be("SOFTWARE_ALREADY_EXISTS");
        (await ErrorCode(delete, HttpStatusCode.Conflict)).Should().Be("SOFTWARE_HAS_LICENSES");
        withSeats.LicenseCount.Should().Be(1);
        withSeats.TotalSeats.Should().Be(5);
    }

    [Fact]
    public async Task LicenseKey_ShouldBeEncryptedHiddenAndRevealedOnlyWithAudit()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var software = await Post<SoftwareData>(admin, "/api/v1/software", NewSoftware(Unique("Win"), "11"));

        var createResponse = await admin.PostAsJsonAsync("/api/v1/licenses", NewLicense(software.Id, quantity: 3, key: PlainKey));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var license = (await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<LicenseData>>(TestJson.Options))!.Data!;
        var listBody = await (await admin.GetAsync($"/api/v1/licenses?softwareId={software.Id}")).Content.ReadAsStringAsync();
        var stored = await factory.WithDbContextAsync(db =>
            db.SoftwareLicenses.Where(l => l.Id == license.Id).Select(l => l.LicenseKey).SingleAsync());

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        license.HasLicenseKey.Should().BeTrue();
        createBody.Should().NotContain(PlainKey);
        listBody.Should().NotContain(PlainKey);
        stored.Should().NotBeNullOrEmpty().And.NotContain(PlainKey, "la clave se guarda cifrada");

        var reveal = await admin.GetAsync($"/api/v1/licenses/{license.Id}/key");
        reveal.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await reveal.Content.ReadFromJsonAsync<ApiEnvelope<KeyData>>(TestJson.Options))!.Data!.LicenseKey.Should().Be(PlainKey);

        var audits = await factory.WithDbContextAsync(db => db.AuditLogs
            .Where(a => a.Module == "Licenses" && a.EntityId == license.Id.ToString())
            .ToListAsync());
        audits.Should().Contain(a => a.Action == AuditAction.SensitiveRead);
        audits.Should().OnlyContain(a => (a.NewValues ?? string.Empty).Contains(PlainKey) == false
            && (a.OldValues ?? string.Empty).Contains(PlainKey) == false);

        // TI_SUPPORT puede ver licencias pero no revelar claves.
        using var support = await CreateUserWithRoleAsync(admin, SystemRoles.TiSupport);
        (await support.GetAsync($"/api/v1/licenses/{license.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await support.GetAsync($"/api/v1/licenses/{license.Id}/key")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateLicenseKey_NullKeepsEmptyClears()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var software = await Post<SoftwareData>(admin, "/api/v1/software", NewSoftware(Unique("Key"), null));
        var license = await Post<LicenseData>(admin, "/api/v1/licenses", NewLicense(software.Id, quantity: 1, key: PlainKey));

        var keep = await Put<LicenseData>(admin, $"/api/v1/licenses/{license.Id}", UpdateBody(software.Id, key: null));
        var clear = await Put<LicenseData>(admin, $"/api/v1/licenses/{license.Id}", UpdateBody(software.Id, key: ""));

        keep.HasLicenseKey.Should().BeTrue();
        clear.HasLicenseKey.Should().BeFalse();
    }

    [Fact]
    public async Task Installations_ShouldPreventOverAllocationAndKeepHistory()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var software = await Post<SoftwareData>(admin, "/api/v1/software", NewSoftware(Unique("Adobe"), "CC"));
        var license = await Post<LicenseData>(admin, "/api/v1/licenses", NewLicense(software.Id, quantity: 2));
        var assetA = await CreateAssetAsync(admin);
        var assetB = await CreateAssetAsync(admin);
        var assetC = await CreateAssetAsync(admin);

        var installA = await Post<InstallationData>(admin, $"/api/v1/licenses/{license.Id}/installations", new { assetId = assetA });
        await Post<InstallationData>(admin, $"/api/v1/licenses/{license.Id}/installations", new { assetId = assetB });
        var exhausted = await admin.PostAsJsonAsync($"/api/v1/licenses/{license.Id}/installations", new { assetId = assetC });
        var duplicate = await admin.PostAsJsonAsync($"/api/v1/licenses/{license.Id}/installations", new { assetId = assetA });
        var full = await Get<LicenseData>(admin, $"/api/v1/licenses/{license.Id}");

        installA.AssetCode.Should().NotBeNullOrEmpty();
        (await ErrorCode(exhausted, HttpStatusCode.Conflict)).Should().Be("LICENSE_EXHAUSTED");
        (await ErrorCode(duplicate, HttpStatusCode.Conflict)).Should().Be("ALREADY_INSTALLED");
        full.UsedQuantity.Should().Be(2);
        full.AvailableQuantity.Should().Be(0);

        (await admin.DeleteAsync($"/api/v1/licenses/{license.Id}/installations/{installA.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var reduceBelowUsage = await admin.PutAsJsonAsync($"/api/v1/licenses/{license.Id}", UpdateBody(software.Id, quantity: 0));
        var deleteInUse = await admin.DeleteAsync($"/api/v1/licenses/{license.Id}");
        var history = await Get<PagedData<InstallationData>>(admin, $"/api/v1/licenses/{license.Id}/installations");
        var activeOnly = await Get<PagedData<InstallationData>>(admin, $"/api/v1/licenses/{license.Id}/installations?activeOnly=true");

        (await ErrorCode(reduceBelowUsage, HttpStatusCode.Conflict)).Should().Be("LICENSE_QUANTITY_BELOW_USAGE");
        (await ErrorCode(deleteInUse, HttpStatusCode.Conflict)).Should().Be("LICENSE_IN_USE");
        history.Items.Should().HaveCount(2);
        history.Items.Should().ContainSingle(i => i.Id == installA.Id && !i.IsActive && i.UninstalledAt != null);
        activeOnly.Items.Should().ContainSingle().Which.AssetId.Should().Be(assetB);
    }

    [Fact]
    public async Task LicenseAlerts_ShouldReportExpiringExpiredExhaustedAndLowUtilization()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var software = await Post<SoftwareData>(admin, "/api/v1/software", NewSoftware(Unique("Alertas"), null));
        var expiring = await Post<LicenseData>(admin, "/api/v1/licenses", NewLicense(software.Id, quantity: 1, expiresInDays: 10));
        var expired = await Post<LicenseData>(admin, "/api/v1/licenses", NewLicense(software.Id, quantity: 1, expiresInDays: -5));
        var lowUse = await Post<LicenseData>(admin, "/api/v1/licenses", NewLicense(software.Id, quantity: 10));
        var exhausted = await Post<LicenseData>(admin, "/api/v1/licenses", NewLicense(software.Id, quantity: 1));
        await Post<InstallationData>(admin, $"/api/v1/licenses/{exhausted.Id}/installations", new { assetId = await CreateAssetAsync(admin) });
        await Post<InstallationData>(admin, $"/api/v1/licenses/{expiring.Id}/installations", new { assetId = await CreateAssetAsync(admin) });

        var alerts = await Get<List<LicenseAlertData>>(admin, "/api/v1/alerts/licenses");

        alerts.Should().ContainSingle(a => a.LicenseId == expiring.Id && a.AlertType == "ExpiringSoon")
            .Which.AlertWindowDays.Should().Be(14);
        alerts.Should().ContainSingle(a => a.LicenseId == expired.Id && a.AlertType == "Expired");
        alerts.Should().ContainSingle(a => a.LicenseId == lowUse.Id && a.AlertType == "LowUtilization");
        alerts.Should().ContainSingle(a => a.LicenseId == exhausted.Id && a.AlertType == "Exhausted");
        alerts.Should().NotContain(a => a.LicenseId == exhausted.Id && a.AlertType == "LowUtilization");
    }

    [Fact]
    public async Task License_WithContractOfAnotherVendor_ShouldReturn400()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var software = await Post<SoftwareData>(admin, "/api/v1/software", NewSoftware(Unique("Mismatch"), null));
        var vendorA = await Post<IdData>(admin, "/api/v1/vendors", NewVendor(Unique("A")));
        var vendorB = await Post<IdData>(admin, "/api/v1/vendors", NewVendor(Unique("B")));
        var contract = await Post<IdData>(admin, "/api/v1/contracts", new
        {
            number = Unique("C").ToUpperInvariant(), name = "Licenciamiento", vendorId = vendorA.Id, type = "Subscription",
            startDate = Today, endDate = Today.AddYears(1)
        });

        var response = await admin.PostAsJsonAsync("/api/v1/licenses",
            NewLicense(software.Id, quantity: 1) with { VendorId = vendorB.Id, ContractId = contract.Id });

        (await ErrorCode(response, HttpStatusCode.BadRequest)).Should().Be("CONTRACT_VENDOR_MISMATCH");
    }

    private async Task<HttpClient> CreateUserWithRoleAsync(HttpClient admin, string role)
    {
        var userName = $"lic{Guid.NewGuid().ToString("N")[..8]}";
        var user = await Post<IdData>(admin, "/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Lic", lastName = "Tester"
        });
        (await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles", new { roles = new[] { role } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        return await factory.LoginFreshAsync(userName, Password);
    }

    private static async Task<int> CreateAssetAsync(HttpClient admin) =>
        (await Post<IdData>(admin, "/api/v1/assets", new
        {
            assetCode = $"L{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}", name = "Equipo", assetTypeId = 1
        })).Id;

    private static object NewSoftware(string name, string? version) =>
        new { name, version, publisher = "Microsoft", category = "Ofimatica", isActive = true };

    private static LicenseBody NewLicense(int softwareId, int quantity, string? key = null, int? expiresInDays = null) =>
        new(softwareId, null, null, $"Licencia {Guid.NewGuid().ToString("N")[..6]}", LicenseType.Subscription, key, quantity,
            Today.AddYears(-1), expiresInDays is { } days ? Today.AddDays(days) : null, 100m);

    private static object UpdateBody(int softwareId, string? key = null, int quantity = 1) => new
    {
        softwareId, name = "Licencia editada", licenseType = "Subscription", licenseKey = key, quantity, isActive = true
    };

    private static object NewVendor(string name) => new { name, status = "Active" };

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, TestJson.Options);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options))!.Data!;
    }

    private static async Task<T> Put<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body, TestJson.Options);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options))!.Data!;
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

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    private sealed record LicenseBody(
        int SoftwareId, int? VendorId, int? ContractId, string Name, LicenseType LicenseType, string? LicenseKey,
        int Quantity, DateOnly? PurchaseDate, DateOnly? ExpirationDate, decimal? Cost);

    private sealed record SoftwareData(int Id, string Name, int LicenseCount, int TotalSeats, int UsedSeats);

    private sealed record LicenseData(int Id, bool HasLicenseKey, int Quantity, int UsedQuantity, int AvailableQuantity);

    private sealed record KeyData(int LicenseId, string? LicenseKey);

    private sealed record InstallationData(int Id, int AssetId, string AssetCode, bool IsActive, DateTime? UninstalledAt);

    private sealed record LicenseAlertData(int LicenseId, string AlertType, int? AlertWindowDays);

    private sealed record IdData(int Id);
}
