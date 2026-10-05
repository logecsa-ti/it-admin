namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TIAdmin.Domain.Enums;
using Xunit;

public sealed class AssetsEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task Create_ShouldStartAvailableWithNormalizedCode()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var code = UniqueCode().ToLowerInvariant();

        var response = await manager.PostAsJsonAsync("/api/v1/assets", NewAsset(code, purchaseDate: "2026-01-10", warranty: "2029-01-10"));

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var asset = (await response.Content.ReadFromJsonAsync<ApiEnvelope<AssetData>>(TestJson.Options))!.Data!;
        asset.AssetCode.Should().Be(code.ToUpperInvariant());
        asset.Status.Should().Be(AssetStatus.Available);
        asset.AssetTypeName.Should().Be("Laptop");
        asset.CurrentAssignment.Should().BeNull();
    }

    [Fact]
    public async Task Create_WithDuplicatesOrInvalidData_ShouldFail()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var code = UniqueCode();
        var serial = $"SN-{UniqueCode()}";
        await manager.PostAsJsonAsync("/api/v1/assets", NewAsset(code, serial: serial));

        var sameCode = await manager.PostAsJsonAsync("/api/v1/assets", NewAsset(code));
        var sameSerial = await manager.PostAsJsonAsync("/api/v1/assets", NewAsset(UniqueCode(), serial: serial));
        var badWarranty = await manager.PostAsJsonAsync("/api/v1/assets",
            NewAsset(UniqueCode(), purchaseDate: "2026-05-01", warranty: "2026-01-01"));
        var unknownType = await manager.PostAsJsonAsync("/api/v1/assets", NewAsset(UniqueCode(), assetTypeId: 999_999));

        (await ErrorCodeAsync(sameCode, HttpStatusCode.Conflict)).Should().Be("ASSET_CODE_ALREADY_EXISTS");
        (await ErrorCodeAsync(sameSerial, HttpStatusCode.Conflict)).Should().Be("SERIAL_NUMBER_ALREADY_EXISTS");
        (await ErrorCodeAsync(badWarranty, HttpStatusCode.BadRequest)).Should().Be("VALIDATION_ERROR");
        (await ErrorCodeAsync(unknownType, HttpStatusCode.BadRequest)).Should().Be("ASSET_TYPE_NOT_AVAILABLE");
    }

    [Fact]
    public async Task AssignmentLifecycle_ShouldPreserveCompleteHistory()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var juan = await CreateUserAsync(admin, "juan");
        var maria = await CreateUserAsync(admin, "maria");
        var asset = await CreateAssetAsync(manager);

        // Juan recibe el activo.
        var assigned = await PostAsync<AssetData>(manager, $"/api/v1/assets/{asset.Id}/assign",
            new { userId = juan.Id, condition = "Nuevo", notes = "Entrega inicial" });
        assigned.Status.Should().Be(AssetStatus.Assigned);
        assigned.CurrentAssignment!.UserId.Should().Be(juan.Id);

        // No puede asignarse dos veces.
        var again = await manager.PostAsJsonAsync($"/api/v1/assets/{asset.Id}/assign", new { userId = maria.Id });
        (await ErrorCodeAsync(again, HttpStatusCode.Conflict)).Should().Be("ASSET_NOT_AVAILABLE");

        // Juan lo devuelve; luego Maria lo recibe.
        var returned = await PostAsync<AssetData>(manager, $"/api/v1/assets/{asset.Id}/return",
            new { condition = "Uso normal", notes = "Cambio de puesto" });
        returned.Status.Should().Be(AssetStatus.Available);
        returned.CurrentAssignment.Should().BeNull();

        await PostAsync<AssetData>(manager, $"/api/v1/assets/{asset.Id}/assign", new { userId = maria.Id });

        var history = await GetAsync<PagedData<AssignmentData>>(manager, $"/api/v1/assets/{asset.Id}/assignments");
        history.Items.Should().HaveCount(2);
        history.Items[0].UserId.Should().Be(maria.Id);
        history.Items[0].IsActive.Should().BeTrue();
        history.Items[1].UserId.Should().Be(juan.Id);
        history.Items[1].IsActive.Should().BeFalse();
        history.Items[1].ReturnDate.Should().NotBeNull();
        history.Items[1].ConditionAtAssignment.Should().Be("Nuevo");
        history.Items[1].ConditionAtReturn.Should().Be("Uso normal");

        var movements = await GetAsync<PagedData<MovementData>>(manager, $"/api/v1/assets/{asset.Id}/movements");
        movements.Items.Select(m => m.MovementType).Should().Equal(
            AssetMovementType.Assignment, AssetMovementType.Return, AssetMovementType.Assignment);
        movements.Items[2].ToValue.Should().Contain(juan.UserName);
        movements.Items[1].FromValue.Should().Contain(juan.UserName);
        movements.Items.Should().OnlyContain(m => m.UserName == TIAdminApiFactory.AssetManagerUserName);
    }

    [Fact]
    public async Task Assign_ToInactiveUser_ShouldReturn400()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, "inactivo");
        await admin.PostAsync($"/api/v1/users/{user.Id}/deactivate", null);
        var asset = await CreateAssetAsync(manager);

        var response = await manager.PostAsJsonAsync($"/api/v1/assets/{asset.Id}/assign", new { userId = user.Id });

        (await ErrorCodeAsync(response, HttpStatusCode.BadRequest)).Should().Be("USER_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Return_NotAssigned_ShouldReturn409_AndFaultyReturnCanGoToRepair()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, "falla");
        var asset = await CreateAssetAsync(manager);

        var notAssigned = await manager.PostAsJsonAsync($"/api/v1/assets/{asset.Id}/return", new { });
        await PostAsync<AssetData>(manager, $"/api/v1/assets/{asset.Id}/assign", new { userId = user.Id });
        var toRepair = await PostAsync<AssetData>(manager, $"/api/v1/assets/{asset.Id}/return",
            new { condition = "No enciende", resultingStatus = "Repair" });

        (await ErrorCodeAsync(notAssigned, HttpStatusCode.Conflict)).Should().Be("ASSET_NOT_ASSIGNED");
        toRepair.Status.Should().Be(AssetStatus.Repair);
    }

    [Fact]
    public async Task ChangeStatus_ShouldFollowRulesAndRecordMovement()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var asset = await CreateAssetAsync(manager);

        var toMaintenance = await manager.PatchAsJsonAsync($"/api/v1/assets/{asset.Id}/status",
            new { status = "Maintenance", notes = "Limpieza preventiva" });
        var toAssigned = await manager.PatchAsJsonAsync($"/api/v1/assets/{asset.Id}/status", new { status = "Assigned" });
        await manager.PatchAsJsonAsync($"/api/v1/assets/{asset.Id}/status", new { status = "Disposed" });
        var fromDisposed = await manager.PatchAsJsonAsync($"/api/v1/assets/{asset.Id}/status", new { status = "Available" });

        toMaintenance.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ErrorCodeAsync(toAssigned, HttpStatusCode.BadRequest)).Should().Be("VALIDATION_ERROR");
        (await ErrorCodeAsync(fromDisposed, HttpStatusCode.Conflict)).Should().Be("INVALID_STATUS_TRANSITION");

        var movements = await GetAsync<PagedData<MovementData>>(manager, $"/api/v1/assets/{asset.Id}/movements");
        movements.Items.Should().Contain(m => m.MovementType == AssetMovementType.StatusChange
            && m.FromValue == "Available" && m.ToValue == "Maintenance" && m.Notes == "Limpieza preventiva");
    }

    [Fact]
    public async Task Update_LocationChange_ShouldRecordMovementWithNames()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var location = await PostAsync<IdData>(admin, "/api/v1/locations", new { code = UniqueCode(), name = "Bodega Central" });
        var asset = await CreateAssetAsync(manager);

        var response = await manager.PutAsJsonAsync($"/api/v1/assets/{asset.Id}", new
        {
            name = "Laptop reasignada", assetTypeId = 1, locationId = location.Id, brand = "Dell", model = "Latitude"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<ApiEnvelope<AssetData>>(TestJson.Options))!.Data!.LocationName.Should().Be("Bodega Central");
        var movements = await GetAsync<PagedData<MovementData>>(manager, $"/api/v1/assets/{asset.Id}/movements");
        movements.Items.Should().ContainSingle(m => m.MovementType == AssetMovementType.LocationChange
            && m.FromValue == null && m.ToValue == "Bodega Central");
    }

    [Fact]
    public async Task Update_ParentCycle_ShouldReturn400()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var pc = await CreateAssetAsync(manager);
        var monitor = await PostAsync<AssetData>(manager, "/api/v1/assets", NewAsset(UniqueCode(), parentAssetId: pc.Id));

        var response = await manager.PutAsJsonAsync($"/api/v1/assets/{pc.Id}",
            new { name = "PC", assetTypeId = 1, parentAssetId = monitor.Id });

        (await ErrorCodeAsync(response, HttpStatusCode.BadRequest)).Should().Be("PARENT_ASSET_CYCLE");
    }

    [Fact]
    public async Task Delete_ShouldRejectAssignedAndKeepHistoryOfDeleted()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, "baja");
        var asset = await CreateAssetAsync(manager);
        await PostAsync<AssetData>(manager, $"/api/v1/assets/{asset.Id}/assign", new { userId = user.Id });

        var assignedDelete = await admin.DeleteAsync($"/api/v1/assets/{asset.Id}");
        await PostAsync<AssetData>(manager, $"/api/v1/assets/{asset.Id}/return", new { });
        var managerDelete = await manager.DeleteAsync($"/api/v1/assets/{asset.Id}");
        var adminDelete = await admin.DeleteAsync($"/api/v1/assets/{asset.Id}");

        (await ErrorCodeAsync(assignedDelete, HttpStatusCode.Conflict)).Should().Be("ASSET_ASSIGNED");
        managerDelete.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TI_ASSET_MANAGER no tiene ASSETS.DELETE");
        adminDelete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/assets/{asset.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var history = await GetAsync<PagedData<AssignmentData>>(admin, $"/api/v1/assignments?assetId={asset.Id}");
        history.Items.Should().ContainSingle().Which.AssetCode.Should().Be(asset.AssetCode);
    }

    [Fact]
    public async Task List_ShouldFilterAndMineShouldOnlyShowOwnAssets()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, "mios");
        var mine = await CreateAssetAsync(manager);
        await CreateAssetAsync(manager);
        await PostAsync<AssetData>(manager, $"/api/v1/assets/{mine.Id}/assign", new { userId = user.Id });

        var byUser = await GetAsync<PagedData<AssetData>>(manager, $"/api/v1/assets?currentUserId={user.Id}");
        var byStatus = await GetAsync<PagedData<AssetData>>(manager, "/api/v1/assets?status=Assigned&pageSize=200");
        var bySearch = await GetAsync<PagedData<AssetData>>(manager, $"/api/v1/assets?search={mine.AssetCode}");
        using var userClient = await factory.LoginFreshAsync(user.UserName, Password);
        var own = await GetAsync<PagedData<AssetData>>(userClient, "/api/v1/assets/mine");
        var inventory = await userClient.GetAsync("/api/v1/assets");
        var ownDetail = await userClient.GetAsync($"/api/v1/assets/{mine.Id}");

        inventory.StatusCode.Should().Be(HttpStatusCode.Forbidden, "el rol USER no ve el inventario completo (Q-10)");
        ownDetail.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        byUser.Items.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
        byUser.Items[0].CurrentUserName.Should().Be("Prueba Activos");
        byStatus.Items.Should().OnlyContain(a => a.Status == AssetStatus.Assigned).And.Contain(a => a.Id == mine.Id);
        bySearch.Items.Should().ContainSingle(a => a.Id == mine.Id);
        own.Items.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task AssetTypes_ShouldListSeedAndBlockInactiveTypesForNewAssets()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var code = $"T_{UniqueCode()}";

        var seeded = await GetAsync<List<AssetTypeData>>(admin, "/api/v1/asset-types");
        var created = await PostAsync<AssetTypeData>(admin, "/api/v1/asset-types", new { code, name = $"Tipo {code}" });
        var duplicate = await admin.PostAsJsonAsync("/api/v1/asset-types", new { code, name = "Otro nombre" });
        var deactivated = await admin.PutAsJsonAsync($"/api/v1/asset-types/{created.Id}",
            new { name = created.Name, description = (string?)null, isActive = false });
        var withInactiveType = await admin.PostAsJsonAsync("/api/v1/assets", NewAsset(UniqueCode(), assetTypeId: created.Id));

        seeded.Select(t => t.Code).Should().Contain(["LAPTOP", "DESKTOP", "MONITOR", "OTHER"]);
        (await ErrorCodeAsync(duplicate, HttpStatusCode.Conflict)).Should().Be("ASSET_TYPE_CODE_ALREADY_EXISTS");
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ErrorCodeAsync(withInactiveType, HttpStatusCode.BadRequest)).Should().Be("ASSET_TYPE_NOT_AVAILABLE");
    }

    [Fact]
    public async Task Create_WithoutAssetsCreate_ShouldReturn403()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, "sinpermiso");
        using var client = await factory.LoginFreshAsync(user.UserName, Password);

        var response = await client.PostAsJsonAsync("/api/v1/assets", NewAsset(UniqueCode()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static object NewAsset(
        string code,
        string? serial = null,
        int assetTypeId = 1,
        string? purchaseDate = null,
        string? warranty = null,
        int? parentAssetId = null) => new
    {
        assetCode = code,
        serialNumber = serial,
        name = "Laptop de prueba",
        assetTypeId,
        brand = "Dell",
        model = "Latitude 5440",
        purchaseDate,
        purchaseCost = 1200.50m,
        warrantyExpiration = warranty,
        parentAssetId
    };

    private async Task<AssetData> CreateAssetAsync(HttpClient client) =>
        await PostAsync<AssetData>(client, "/api/v1/assets", NewAsset(UniqueCode()));

    private static async Task<UserData> CreateUserAsync(HttpClient admin, string prefix)
    {
        var userName = $"{prefix}{Guid.NewGuid().ToString("N")[..8]}";
        return await PostAsync<UserData>(admin, "/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Prueba", lastName = "Activos"
        });
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options))!.Data!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options))!.Data!;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(TestJson.Options))!.Errors!.First().Code;
    }

    private static string UniqueCode() => $"A{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private sealed record AssetData(
        int Id,
        string AssetCode,
        string AssetTypeName,
        AssetStatus Status,
        string? CurrentUserName,
        string? LocationName,
        AssignmentData? CurrentAssignment);

    private sealed record AssignmentData(
        int Id,
        string AssetCode,
        int UserId,
        bool IsActive,
        DateTime? ReturnDate,
        string? ConditionAtAssignment,
        string? ConditionAtReturn);

    private sealed record MovementData(AssetMovementType MovementType, string? FromValue, string? ToValue, string? UserName, string? Notes);

    private sealed record AssetTypeData(int Id, string Code, string Name, bool IsActive);

    private sealed record UserData(int Id, string UserName);

    private sealed record IdData(int Id);
}
