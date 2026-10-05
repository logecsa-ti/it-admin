namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TIAdmin.Domain.Enums;
using Xunit;

public sealed class VendorsContractsEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Vendor_Crud_ShouldEnforceUniqueNameAndBlockDeleteWithOpenContracts()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var name = Unique("Proveedor");

        var created = await Post<VendorData>(admin, "/api/v1/vendors", NewVendor(name));
        var duplicate = await admin.PostAsJsonAsync("/api/v1/vendors", NewVendor(name));
        var badEmail = await admin.PostAsJsonAsync("/api/v1/vendors", NewVendor(Unique("V")) with { Email = "no-es-correo" });
        var contract = await Post<ContractData>(admin, "/api/v1/contracts", NewContract(created.Id, activate: true));

        var blockedDelete = await admin.DeleteAsync($"/api/v1/vendors/{created.Id}");
        await admin.PostAsync($"/api/v1/contracts/{contract.Id}/terminate", null);
        var delete = await admin.DeleteAsync($"/api/v1/vendors/{created.Id}");
        var reuseName = await admin.PostAsJsonAsync("/api/v1/vendors", NewVendor(name));

        created.Status.Should().Be(VendorStatus.Active);
        (await ErrorCode(duplicate, HttpStatusCode.Conflict)).Should().Be("VENDOR_NAME_ALREADY_EXISTS");
        (await ErrorCode(badEmail, HttpStatusCode.BadRequest)).Should().Be("VALIDATION_ERROR");
        (await ErrorCode(blockedDelete, HttpStatusCode.Conflict)).Should().Be("VENDOR_HAS_OPEN_CONTRACTS");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
        reuseName.StatusCode.Should().Be(HttpStatusCode.Created, "un proveedor eliminado libera su nombre");

        // El historial de contratos del proveedor eliminado sigue visible y con su nombre.
        var history = await Get<ContractData>(admin, $"/api/v1/contracts/{contract.Id}");
        history.VendorName.Should().Be(name);
        history.Status.Should().Be(ContractStatus.Terminated);
    }

    [Fact]
    public async Task Contract_Lifecycle_ShouldDeriveStatusAndKeepRenewalHistory()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var vendor = await Post<VendorData>(admin, "/api/v1/vendors", NewVendor(Unique("Lifecycle")));

        var draft = await Post<ContractData>(admin, "/api/v1/contracts", NewContract(vendor.Id, endInDays: 200));
        var active = await Post<ContractData>(admin, $"/api/v1/contracts/{draft.Id}/activate", new { });
        var expiring = await Post<ContractData>(admin, "/api/v1/contracts", NewContract(vendor.Id, endInDays: 10, activate: true));

        draft.Status.Should().Be(ContractStatus.Draft);
        active.Status.Should().Be(ContractStatus.Active);
        expiring.Status.Should().Be(ContractStatus.Expiring);
        expiring.DaysRemaining.Should().BeInRange(9, 11);

        var successorNumber = Unique("REN").ToUpperInvariant();
        var renewResponse = await admin.PostAsJsonAsync($"/api/v1/contracts/{expiring.Id}/renew", new
        {
            number = successorNumber,
            startDate = expiring.EndDate.AddDays(1),
            endDate = expiring.EndDate.AddYears(1),
            value = 2500m
        });
        renewResponse.StatusCode.Should().Be(HttpStatusCode.Created, await renewResponse.Content.ReadAsStringAsync());
        var successor = (await renewResponse.Content.ReadFromJsonAsync<ApiEnvelope<ContractData>>(TestJson.Options))!.Data!;
        var original = await Get<ContractData>(admin, $"/api/v1/contracts/{expiring.Id}");

        successor.Status.Should().Be(ContractStatus.Active);
        successor.RenewedFromContractId.Should().Be(expiring.Id);
        original.Status.Should().Be(ContractStatus.Renewed);

        var editClosed = await admin.PutAsJsonAsync($"/api/v1/contracts/{expiring.Id}", UpdateBody(vendor.Id));
        var renewAgain = await admin.PostAsJsonAsync($"/api/v1/contracts/{successor.Id}/renew",
            new { number = successorNumber, startDate = Today.AddYears(1), endDate = Today.AddYears(2) });
        var deleteActive = await admin.DeleteAsync($"/api/v1/contracts/{successor.Id}");

        (await ErrorCode(editClosed, HttpStatusCode.Conflict)).Should().Be("CONTRACT_CLOSED");
        (await ErrorCode(renewAgain, HttpStatusCode.Conflict)).Should().Be("CONTRACT_NUMBER_ALREADY_EXISTS");
        (await ErrorCode(deleteActive, HttpStatusCode.Conflict)).Should().Be("CONTRACT_ACTIVE");
    }

    [Fact]
    public async Task Contract_StatusFilterAndAlerts_ShouldUseEffectiveStatusAndConfiguredWindows()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var vendor = await Post<VendorData>(admin, "/api/v1/vendors", NewVendor(Unique("Alertas")));
        var in20 = await Post<ContractData>(admin, "/api/v1/contracts", NewContract(vendor.Id, endInDays: 20, activate: true));
        var in200 = await Post<ContractData>(admin, "/api/v1/contracts", NewContract(vendor.Id, endInDays: 200, activate: true));
        var expired = await Post<ContractData>(admin, "/api/v1/contracts", NewContract(vendor.Id, endInDays: -5, activate: true));

        var expiringList = await Get<PagedData<ContractData>>(admin, $"/api/v1/contracts?vendorId={vendor.Id}&status=Expiring");
        var expiredList = await Get<PagedData<ContractData>>(admin, $"/api/v1/contracts?vendorId={vendor.Id}&status=Expired");
        var alerts = await Get<List<ContractAlertData>>(admin, "/api/v1/alerts/contracts");

        expiringList.Items.Select(c => c.Id).Should().Equal(in20.Id);
        expiredList.Items.Select(c => c.Id).Should().Equal(expired.Id);
        alerts.Should().ContainSingle(a => a.ContractId == in20.Id).Which.AlertWindowDays.Should().Be(30);
        alerts.Should().ContainSingle(a => a.ContractId == expired.Id).Which.IsExpired.Should().BeTrue();
        alerts.Should().NotContain(a => a.ContractId == in200.Id, "fuera de la ventana maxima (90 dias)");
    }

    [Fact]
    public async Task Contract_WithBlockedVendor_ShouldReturn400()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var vendor = await Post<VendorData>(admin, "/api/v1/vendors", NewVendor(Unique("Bloqueado")) with { Status = VendorStatus.Blocked });

        var response = await admin.PostAsJsonAsync("/api/v1/contracts", NewContract(vendor.Id));

        (await ErrorCode(response, HttpStatusCode.BadRequest)).Should().Be("VENDOR_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Asset_WithVendor_ShouldExposeVendorName()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var vendor = await Post<VendorData>(admin, "/api/v1/vendors", NewVendor(Unique("Dell")));

        var asset = await Post<AssetVendorData>(admin, "/api/v1/assets", new
        {
            assetCode = Unique("AV"), name = "Laptop con proveedor", assetTypeId = 1, vendorId = vendor.Id
        });

        asset.VendorId.Should().Be(vendor.Id);
        asset.VendorName.Should().Be(vendor.Name);
    }

    [Fact]
    public async Task Contracts_WithoutManagePermission_ShouldReturn403()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();

        var list = await manager.GetAsync("/api/v1/contracts");
        var create = await manager.PostAsJsonAsync("/api/v1/contracts", NewContract(1));

        list.StatusCode.Should().Be(HttpStatusCode.OK, "TI_ASSET_MANAGER tiene CONTRACTS.VIEW");
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static VendorBody NewVendor(string name) => new(null, name, "J0310000000001", "Contacto", "ventas@proveedor.test",
        "+505 2222 0000", null, "Managua", "Nicaragua", null, VendorStatus.Active, null, 4.5m);

    private static object NewContract(int vendorId, int endInDays = 365, bool activate = false) => new
    {
        number = Unique("CT").ToUpperInvariant(),
        name = "Soporte anual",
        vendorId,
        type = "Support",
        startDate = Today.AddYears(-1),
        endDate = Today.AddDays(endInDays),
        value = 1200m,
        currency = "usd",
        autoRenew = false,
        renewalNoticeDays = 30,
        activate
    };

    private static object UpdateBody(int vendorId) => new
    {
        name = "Editado",
        vendorId,
        type = "Support",
        startDate = Today.AddYears(-1),
        endDate = Today.AddYears(1)
    };

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
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

    private sealed record VendorBody(
        string? Code, string Name, string? TaxId, string? ContactName, string? Email, string? Phone, string? Address,
        string? City, string? Country, string? Website, VendorStatus Status, string? Notes, decimal? Rating);

    private sealed record VendorData(int Id, string Name, VendorStatus Status);

    private sealed record ContractData(
        int Id, string Number, int VendorId, string VendorName, DateOnly EndDate, ContractStatus Status,
        int DaysRemaining, int? RenewedFromContractId);

    private sealed record ContractAlertData(int ContractId, int DaysRemaining, int? AlertWindowDays, bool IsExpired);

    private sealed record AssetVendorData(int Id, int? VendorId, string? VendorName);
}
