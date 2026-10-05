namespace TIAdmin.Tests.Unit;

using FluentAssertions;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;
using Xunit;

public class MaintenanceTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Lifecycle_ShouldScheduleStartAndCompleteWithResults()
    {
        var maintenance = NewMaintenance();

        maintenance.Schedule(Now.AddDays(2));
        maintenance.Start(Now.AddDays(2));
        maintenance.Complete(Now.AddDays(2).AddHours(3), "Limpieza y cambio de pasta termica", "Polvo acumulado", "Repetir en 6 meses",
            85m, new DateOnly(2027, 4, 5));

        maintenance.Status.Should().Be(MaintenanceStatus.Completed);
        maintenance.StartedAt.Should().Be(Now.AddDays(2));
        maintenance.CompletedAt.Should().Be(Now.AddDays(2).AddHours(3));
        maintenance.ActualCost.Should().Be(85m);
        maintenance.NextDueDate.Should().Be(new DateOnly(2027, 4, 5));
        maintenance.Invoking(m => m.Cancel("tarde"))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_MAINTENANCE_TRANSITION");
    }

    [Fact]
    public void Complete_ShouldRequireInProgressAndActions()
    {
        var planned = NewMaintenance();
        var started = NewMaintenance();
        started.Start(Now);

        planned.Invoking(m => m.Complete(Now, "Accion", null, null, null, null))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_MAINTENANCE_TRANSITION");
        started.Invoking(m => m.Complete(Now, " ", null, null, null, null))
            .Should().Throw<DomainValidationException>().Which.Code.Should().Be("MAINTENANCE_ACTIONS_REQUIRED");
    }

    [Fact]
    public void Cancel_ShouldRequireReasonAndBlockFurtherWork()
    {
        var maintenance = NewMaintenance();

        maintenance.Invoking(m => m.Cancel("")).Should().Throw<DomainValidationException>();
        maintenance.Cancel("Equipo dado de baja");

        maintenance.Status.Should().Be(MaintenanceStatus.Cancelled);
        maintenance.Invoking(m => m.Start(Now)).Should().Throw<ConflictException>();
    }

    private static Maintenance NewMaintenance() =>
        new() { Number = "MNT-2026-000001", Title = "Preventivo", Type = MaintenanceType.Preventive, AssetId = 1, ScheduledDate = Now };
}

public class ChangeRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NormalChange_ShouldFollowReviewAndImplementationFlow()
    {
        var change = NewChange(ChangeType.Normal, rollback: "Restaurar snapshot");

        change.Submit(Now);
        change.StartReview();
        change.Approve(approverId: 5, Now, "Ventana del sabado");
        change.Assign(9);
        change.StartImplementation(Now.AddDays(3));
        change.Complete(Now.AddDays(3).AddHours(2), "Sin incidentes");
        change.Close(Now.AddDays(4));

        change.Status.Should().Be(ChangeStatus.Closed);
        change.ApprovedById.Should().Be(5);
        change.ImplementationDate.Should().Be(Now.AddDays(3));
        change.ImplementationNotes.Should().Be("Sin incidentes");
        change.ClosedAt.Should().Be(Now.AddDays(4));
    }

    [Fact]
    public void Submit_WithoutRollbackPlan_ShouldThrowUnlessStandard()
    {
        var normal = NewChange(ChangeType.Normal, rollback: null);
        var standard = NewChange(ChangeType.Standard, rollback: null);

        normal.Invoking(c => c.Submit(Now)).Should().Throw<DomainValidationException>().Which.Code.Should().Be("ROLLBACK_PLAN_REQUIRED");
        standard.Submit(Now);

        standard.Status.Should().Be(ChangeStatus.Approved, "un cambio estandar esta preaprobado");
        standard.ApprovedAt.Should().Be(Now);
    }

    [Fact]
    public void Requester_ShouldNotApproveOrRejectOwnChange()
    {
        var change = NewChange(ChangeType.Emergency, rollback: "Revertir");
        change.Submit(Now);

        change.Invoking(c => c.Approve(change.RequestedById, Now, null))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("SELF_APPROVAL_NOT_ALLOWED");
        change.Invoking(c => c.Reject(change.RequestedById, Now, "no"))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("SELF_APPROVAL_NOT_ALLOWED");
    }

    [Fact]
    public void RollBack_ShouldRequireNotesAndAllowClosing()
    {
        var change = NewChange(ChangeType.Normal, rollback: "Revertir");
        change.Submit(Now);
        change.Approve(5, Now, null);
        change.StartImplementation(Now);

        change.Invoking(c => c.RollBack(Now, "")).Should().Throw<DomainValidationException>();
        change.RollBack(Now.AddHours(1), "Fallo la migracion de datos");
        change.Close(Now.AddHours(2));

        change.Status.Should().Be(ChangeStatus.Closed);
        change.ImplementationNotes.Should().Be("Fallo la migracion de datos");
    }

    [Fact]
    public void InvalidTransitions_ShouldThrow()
    {
        var change = NewChange(ChangeType.Normal, rollback: "Revertir");

        change.Invoking(c => c.StartImplementation(Now)).Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_CHANGE_TRANSITION");
        change.Submit(Now);
        change.Invoking(c => c.EnsureEditable()).Should().Throw<ConflictException>().Which.Code.Should().Be("CHANGE_NOT_EDITABLE");
        change.Invoking(c => c.Close(Now)).Should().Throw<ConflictException>();
    }

    private static ChangeRequest NewChange(ChangeType type, string? rollback) => new()
    {
        Number = "CHG-2026-000001", Title = "Actualizar servidor", Description = "Parche de seguridad", Type = type,
        Risk = ChangeRisk.Medium, Impact = ChangeImpact.Medium, RequestedById = 1, RollbackPlan = rollback
    };
}

public class PurchaseRequestTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ReplaceItems_ShouldComputeTotals()
    {
        var purchase = NewPurchase();

        purchase.ReplaceItems([Item("Laptop", 3, 950m), Item("Mouse", 3, 15.5m)]);

        purchase.Items.Should().HaveCount(2);
        purchase.Items.Select(i => i.TotalPrice).Should().Equal(2850m, 46.5m);
        purchase.EstimatedCost.Should().Be(2896.5m);
    }

    [Fact]
    public void ReplaceItems_InvalidQuantityOrPrice_ShouldThrow()
    {
        var purchase = NewPurchase();

        purchase.Invoking(p => p.ReplaceItems([Item("X", 0, 10m)]))
            .Should().Throw<DomainValidationException>().Which.Code.Should().Be("INVALID_ITEM_QUANTITY");
        purchase.Invoking(p => p.ReplaceItems([Item("X", 1, -1m)]))
            .Should().Throw<DomainValidationException>().Which.Code.Should().Be("INVALID_ITEM_PRICE");
    }

    [Fact]
    public void Lifecycle_ShouldRequireItemsVendorAndSegregationOfDuties()
    {
        var purchase = NewPurchase();

        purchase.Invoking(p => p.Submit()).Should().Throw<DomainValidationException>().Which.Code.Should().Be("PURCHASE_ITEMS_REQUIRED");
        purchase.ReplaceItems([Item("Switch 24p", 1, 400m)]);
        purchase.Submit();
        purchase.Invoking(p => p.ReplaceItems([])).Should().Throw<ConflictException>().Which.Code.Should().Be("PURCHASE_NOT_EDITABLE");
        purchase.Invoking(p => p.Approve(purchase.RequestedById, Now))
            .Should().Throw<ConflictException>().Which.Code.Should().Be("SELF_APPROVAL_NOT_ALLOWED");

        purchase.Approve(approverId: 7, Now);
        purchase.Invoking(p => p.MarkOrdered(Now))
            .Should().Throw<DomainValidationException>().Which.Code.Should().Be("PURCHASE_VENDOR_REQUIRED");
        purchase.VendorId = 3;
        purchase.MarkOrdered(Now.AddDays(1));
        purchase.MarkReceived(Now.AddDays(10));

        purchase.Status.Should().Be(PurchaseStatus.Received);
        purchase.ApprovedById.Should().Be(7);
        purchase.ReceivedAt.Should().Be(Now.AddDays(10));
        purchase.Invoking(p => p.Cancel(Now)).Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_PURCHASE_TRANSITION");
    }

    [Fact]
    public void Order_WithVendorOnEveryItem_ShouldNotRequireRequestVendor()
    {
        var purchase = NewPurchase();
        var item = Item("Licencia", 10, 20m);
        item.VendorId = 4;
        purchase.ReplaceItems([item]);
        purchase.Submit();
        purchase.Approve(7, Now);

        purchase.Invoking(p => p.MarkOrdered(Now)).Should().NotThrow();
    }

    private static PurchaseRequest NewPurchase() =>
        new() { Number = "PUR-2026-000001", Title = "Equipos", RequestedById = 1, RequestedDate = new DateOnly(2026, 10, 5) };

    private static PurchaseItem Item(string description, int quantity, decimal price) =>
        new() { Description = description, Quantity = quantity, UnitPrice = price };
}
