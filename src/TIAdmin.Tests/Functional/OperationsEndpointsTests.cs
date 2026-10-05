namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Enums;
using Xunit;

public sealed class OperationsEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task Maintenance_Lifecycle_ShouldMoveAssetToMaintenanceAndBack()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var asset = await CreateAssetAsync(manager);

        var created = await Post<MaintenanceData>(manager, "/api/v1/maintenances", NewMaintenance(asset, DateTime.UtcNow.AddDays(1)));
        await Post<MaintenanceData>(manager, $"/api/v1/maintenances/{created.Id}/start", new { });
        var assetDuring = await Get<AssetStatusData>(manager, $"/api/v1/assets/{asset}");
        var completed = await Post<MaintenanceData>(manager, $"/api/v1/maintenances/{created.Id}/complete", new
        {
            actions = "Limpieza interna", findings = "Ventilador ruidoso", actualCost = 40m, nextDueDate = "2027-04-01"
        });
        var assetAfter = await Get<AssetStatusData>(manager, $"/api/v1/assets/{asset}");
        var movements = await Get<PagedData<MovementData>>(manager, $"/api/v1/assets/{asset}/movements");
        var deleteCompleted = await manager.DeleteAsync($"/api/v1/maintenances/{created.Id}");

        created.Number.Should().MatchRegex(@"^MNT-\d{4}-\d{6}$");
        created.Status.Should().Be(MaintenanceStatus.Planned);
        assetDuring.Status.Should().Be(AssetStatus.Maintenance);
        completed.Status.Should().Be(MaintenanceStatus.Completed);
        completed.ActualCost.Should().Be(40m);
        assetAfter.Status.Should().Be(AssetStatus.Available);
        movements.Items.Where(m => m.MovementType == AssetMovementType.StatusChange).Should().HaveCount(2);
        (await ErrorCode(deleteCompleted, HttpStatusCode.Conflict)).Should().Be("MAINTENANCE_NOT_DELETABLE");
    }

    [Fact]
    public async Task Maintenance_OnAssignedAsset_ShouldNotChangeItsStatus()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var holder = await CreateUserAsync(SystemRoles.User);
        var asset = await CreateAssetAsync(manager);
        await Post<object>(manager, $"/api/v1/assets/{asset}/assign", new { userId = holder.Id });
        var maintenance = await Post<MaintenanceData>(manager, "/api/v1/maintenances", NewMaintenance(asset, DateTime.UtcNow));

        await Post<MaintenanceData>(manager, $"/api/v1/maintenances/{maintenance.Id}/start", new { });
        var cancelWithoutReason = await manager.PostAsJsonAsync($"/api/v1/maintenances/{maintenance.Id}/cancel", new { reason = "" });
        await Post<MaintenanceData>(manager, $"/api/v1/maintenances/{maintenance.Id}/cancel", new { reason = "Usuario de viaje" });

        (await Get<AssetStatusData>(manager, $"/api/v1/assets/{asset}")).Status.Should().Be(AssetStatus.Assigned);
        (await ErrorCode(cancelWithoutReason, HttpStatusCode.BadRequest)).Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Maintenance_TechnicianMustBeQualifiedAndAlertsShouldReportDueWork()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var endUser = await CreateUserAsync(SystemRoles.User);
        var asset = await CreateAssetAsync(manager);

        var unqualified = await manager.PostAsJsonAsync("/api/v1/maintenances",
            NewMaintenance(asset, DateTime.UtcNow.AddDays(1)) with { TechnicianId = endUser.Id });
        var upcoming = await Post<MaintenanceData>(manager, "/api/v1/maintenances", NewMaintenance(asset, DateTime.UtcNow.AddDays(3)));
        var overdue = await Post<MaintenanceData>(manager, "/api/v1/maintenances", NewMaintenance(await CreateAssetAsync(manager), DateTime.UtcNow.AddDays(-2)));

        // Preventivo completado cuyo siguiente vencimiento ya llego, en un activo sin otro mantenimiento abierto.
        var preventiveAsset = await CreateAssetAsync(manager);
        var done = await Post<MaintenanceData>(manager, "/api/v1/maintenances", NewMaintenance(preventiveAsset, DateTime.UtcNow.AddDays(-200)));
        await Post<MaintenanceData>(manager, $"/api/v1/maintenances/{done.Id}/start", new { });
        await Post<MaintenanceData>(manager, $"/api/v1/maintenances/{done.Id}/complete",
            new { actions = "Preventivo", nextDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)) });

        var alerts = await Get<List<MaintenanceAlertData>>(manager, "/api/v1/alerts/maintenance");
        var overdueList = await Get<PagedData<MaintenanceData>>(manager, "/api/v1/maintenances?overdue=true&pageSize=200");

        (await ErrorCode(unqualified, HttpStatusCode.BadRequest)).Should().Be("TECHNICIAN_NOT_QUALIFIED");
        alerts.Should().Contain(a => a.MaintenanceId == upcoming.Id && a.AlertType == MaintenanceAlertType.Upcoming);
        alerts.Should().Contain(a => a.MaintenanceId == overdue.Id && a.AlertType == MaintenanceAlertType.Overdue);
        alerts.Should().Contain(a => a.AssetId == preventiveAsset && a.AlertType == MaintenanceAlertType.PreventiveDue);
        overdueList.Items.Should().Contain(m => m.Id == overdue.Id && m.IsOverdue).And.NotContain(m => m.Id == upcoming.Id);
    }

    [Fact]
    public async Task Change_ShouldRequireReviewBySomeoneElseAndLetAssigneeImplement()
    {
        var requester = await CreateUserAsync(SystemRoles.TiSupport);
        var implementer = await CreateUserAsync(SystemRoles.TiSupport);
        var reviewer = await CreateUserAsync(SystemRoles.TiManager);

        var draft = await Post<ChangeData>(requester.Client, "/api/v1/changes", NewChange("Normal", rollback: null));
        var submitWithoutRollback = await requester.Client.PostAsJsonAsync($"/api/v1/changes/{draft.Id}/submit", new { });
        await Put<ChangeData>(requester.Client, $"/api/v1/changes/{draft.Id}", NewChange("Normal", rollback: "Restaurar backup de la VM"));
        var submitted = await Post<ChangeData>(requester.Client, $"/api/v1/changes/{draft.Id}/submit", new { });
        var supportApprove = await requester.Client.PostAsJsonAsync($"/api/v1/changes/{draft.Id}/approve", new { });
        await Post<ChangeData>(reviewer.Client, $"/api/v1/changes/{draft.Id}/review", new { });
        var approved = await Post<ChangeData>(reviewer.Client, $"/api/v1/changes/{draft.Id}/approve", new { comment = "OK para el sabado" });
        var notAssignedStart = await implementer.Client.PostAsJsonAsync($"/api/v1/changes/{draft.Id}/start", new { });
        await Post<ChangeData>(reviewer.Client, $"/api/v1/changes/{draft.Id}/assign", new { userId = implementer.Id });
        await Post<ChangeData>(implementer.Client, $"/api/v1/changes/{draft.Id}/start", new { });
        var completed = await Post<ChangeData>(implementer.Client, $"/api/v1/changes/{draft.Id}/complete", new { notes = "Aplicado" });
        var closed = await Post<ChangeData>(reviewer.Client, $"/api/v1/changes/{draft.Id}/close", new { });

        draft.Number.Should().MatchRegex(@"^CHG-\d{4}-\d{6}$");
        (await ErrorCode(submitWithoutRollback, HttpStatusCode.BadRequest)).Should().Be("ROLLBACK_PLAN_REQUIRED");
        submitted.Status.Should().Be(ChangeStatus.Requested);
        supportApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TI_SUPPORT no tiene CHANGES.REVIEW");
        approved.ApprovedByName.Should().NotBeNullOrEmpty();
        notAssignedStart.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        completed.Status.Should().Be(ChangeStatus.Completed);
        closed.Status.Should().Be(ChangeStatus.Closed);

        var audits = await factory.WithDbContextAsync(db => db.AuditLogs
            .Where(a => a.Module == "Changes" && a.EntityId == draft.Id.ToString()).CountAsync());
        audits.Should().BeGreaterThan(5, "cada transicion queda auditada en el modulo de cambios");
    }

    [Fact]
    public async Task Change_ReviewerCannotApproveOwnChange_AndStandardIsPreApproved()
    {
        // TI_ADMIN puede crear y revisar cambios; aun asi no aprueba los propios.
        var manager = await CreateUserAsync(SystemRoles.TiAdmin);

        var own = await Post<ChangeData>(manager.Client, "/api/v1/changes", NewChange("Emergency", rollback: "Revertir"));
        await Post<ChangeData>(manager.Client, $"/api/v1/changes/{own.Id}/submit", new { });
        var selfApprove = await manager.Client.PostAsJsonAsync($"/api/v1/changes/{own.Id}/approve", new { });

        var standard = await Post<ChangeData>(manager.Client, "/api/v1/changes", NewChange("Standard", rollback: null));
        var preApproved = await Post<ChangeData>(manager.Client, $"/api/v1/changes/{standard.Id}/submit", new { });

        (await ErrorCode(selfApprove, HttpStatusCode.Conflict)).Should().Be("SELF_APPROVAL_NOT_ALLOWED");
        preApproved.Status.Should().Be(ChangeStatus.Approved);
    }

    [Fact]
    public async Task Purchase_Lifecycle_ShouldTotalItemsAndEnforceApprovalRules()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var approver = await CreateUserAsync(SystemRoles.TiManager);
        var vendor = await Post<IdData>(admin, "/api/v1/vendors", new { name = $"Proveedor {Guid.NewGuid():N}"[..30], status = "Active" });

        var created = await Post<PurchaseData>(admin, "/api/v1/purchases", new
        {
            title = "Renovacion de laptops",
            justification = "Equipos con mas de 5 anios",
            items = new[]
            {
                new { description = "Laptop 14\"", quantity = 3, unitPrice = 950m, assetTypeId = (int?)1 },
                new { description = "Docking station", quantity = 3, unitPrice = 120m, assetTypeId = (int?)null }
            }
        });
        await Post<PurchaseData>(admin, $"/api/v1/purchases/{created.Id}/submit", new { });
        var selfApprove = await admin.PostAsJsonAsync($"/api/v1/purchases/{created.Id}/approve", new { });
        var approved = await Post<PurchaseData>(approver.Client, $"/api/v1/purchases/{created.Id}/approve", new { });
        var orderWithoutVendor = await admin.PostAsJsonAsync($"/api/v1/purchases/{created.Id}/order", new { });
        var approverOrders = await approver.Client.PostAsJsonAsync($"/api/v1/purchases/{created.Id}/order", new { });

        created.Number.Should().MatchRegex(@"^PUR-\d{4}-\d{6}$");
        created.EstimatedCost.Should().Be(3210m);
        created.Items.Should().HaveCount(2);
        (await ErrorCode(selfApprove, HttpStatusCode.Conflict)).Should().Be("SELF_APPROVAL_NOT_ALLOWED");
        approved.Status.Should().Be(PurchaseStatus.Approved);
        (await ErrorCode(orderWithoutVendor, HttpStatusCode.BadRequest)).Should().Be("PURCHASE_VENDOR_REQUIRED");
        approverOrders.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TI_MANAGER aprueba pero no gestiona compras");

        // Una compra en borrador con proveedor sigue el ciclo completo.
        var withVendor = await Post<PurchaseData>(admin, "/api/v1/purchases", new
        {
            title = "Switches", vendorId = vendor.Id,
            items = new[] { new { description = "Switch 24p", quantity = 2, unitPrice = 400m } }
        });
        await Post<PurchaseData>(admin, $"/api/v1/purchases/{withVendor.Id}/submit", new { });
        await Post<PurchaseData>(approver.Client, $"/api/v1/purchases/{withVendor.Id}/approve", new { });
        await Post<PurchaseData>(admin, $"/api/v1/purchases/{withVendor.Id}/order", new { });
        var received = await Post<PurchaseData>(admin, $"/api/v1/purchases/{withVendor.Id}/receive", new { });

        received.Status.Should().Be(PurchaseStatus.Received);
        received.VendorName.Should().StartWith("Proveedor");
        received.ReceivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Purchase_DraftEditsReplaceItemsAndSubmittedCannotBeEdited()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var draft = await Post<PurchaseData>(admin, "/api/v1/purchases", new { title = "Borrador", items = Array.Empty<object>() });
        var emptySubmit = await admin.PostAsJsonAsync($"/api/v1/purchases/{draft.Id}/submit", new { });
        var edited = await Put<PurchaseData>(admin, $"/api/v1/purchases/{draft.Id}", new
        {
            title = "Borrador editado", items = new[] { new { description = "Monitor", quantity = 2, unitPrice = 180m } }
        });
        await Post<PurchaseData>(admin, $"/api/v1/purchases/{draft.Id}/submit", new { });
        var editSubmitted = await admin.PutAsJsonAsync($"/api/v1/purchases/{draft.Id}", new { title = "x", items = Array.Empty<object>() });

        (await ErrorCode(emptySubmit, HttpStatusCode.BadRequest)).Should().Be("PURCHASE_ITEMS_REQUIRED");
        edited.EstimatedCost.Should().Be(360m);
        edited.Items.Should().ContainSingle().Which.Description.Should().Be("Monitor");
        (await ErrorCode(editSubmitted, HttpStatusCode.Conflict)).Should().Be("PURCHASE_NOT_EDITABLE");
    }

    private async Task<TestUser> CreateUserAsync(string role)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var userName = $"op{Guid.NewGuid().ToString("N")[..8]}";
        var user = await Post<IdData>(admin, "/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Op", lastName = role
        });

        if (role != SystemRoles.User)
        {
            (await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles", new { roles = new[] { role } }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return new TestUser(user.Id, await factory.LoginFreshAsync(userName, Password));
    }

    private static async Task<int> CreateAssetAsync(HttpClient client) =>
        (await Post<IdData>(client, "/api/v1/assets", new
        {
            assetCode = $"OP{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}", name = "Equipo", assetTypeId = 1
        })).Id;

    private static MaintenanceBody NewMaintenance(int assetId, DateTime scheduled) =>
        new("Mantenimiento preventivo", null, "Preventive", assetId, null, null, scheduled, 50m);

    private static object NewChange(string type, string? rollback) => new
    {
        title = "Actualizar firmware del firewall",
        description = "Version 7.2.5 con parches de seguridad",
        type,
        risk = "Medium",
        impact = "High",
        plannedDate = DateTime.UtcNow.AddDays(5),
        rollbackPlan = rollback
    };

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

    private sealed record TestUser(int Id, HttpClient Client);

    private sealed record MaintenanceBody(
        string Title, string? Description, string Type, int AssetId, int? TechnicianId, int? VendorId, DateTime ScheduledDate, decimal? EstimatedCost);

    private sealed record MaintenanceData(int Id, string Number, MaintenanceStatus Status, decimal? ActualCost, bool IsOverdue);

    private sealed record MaintenanceAlertData(MaintenanceAlertType AlertType, int AssetId, int? MaintenanceId);

    private sealed record AssetStatusData(int Id, AssetStatus Status);

    private sealed record MovementData(AssetMovementType MovementType, string? FromValue, string? ToValue);

    private sealed record ChangeData(int Id, string Number, ChangeStatus Status, string? ApprovedByName);

    private sealed record PurchaseItemData(string Description, int Quantity, decimal TotalPrice);

    private sealed record PurchaseData(
        int Id, string Number, PurchaseStatus Status, decimal EstimatedCost, string? VendorName, DateTime? ReceivedAt, List<PurchaseItemData> Items);

    private sealed record IdData(int Id);
}
