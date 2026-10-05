namespace TIAdmin.Tests.Unit;

using FluentAssertions;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;
using Xunit;

/// <summary>
/// Reglas de estado y asignacion del activo (SPECS.md secciones 19-20), sin base de datos.
/// </summary>
public class AssetTests
{
    private static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AssignTo_AvailableAsset_ShouldCreateActiveAssignmentAndMarkAssigned()
    {
        var asset = NewAsset();

        var assignment = asset.AssignTo(userId: 5, assignedById: 1, Now, "Nuevo", "Entrega inicial");

        asset.Status.Should().Be(AssetStatus.Assigned);
        asset.CurrentUserId.Should().Be(5);
        assignment.IsActive.Should().BeTrue();
        assignment.UserId.Should().Be(5);
        assignment.AssignedById.Should().Be(1);
        assignment.AssignmentDate.Should().Be(Now);
        assignment.ConditionAtAssignment.Should().Be("Nuevo");
        assignment.ReturnDate.Should().BeNull();
    }

    [Theory]
    [InlineData(AssetStatus.Maintenance)]
    [InlineData(AssetStatus.Repair)]
    [InlineData(AssetStatus.Retired)]
    [InlineData(AssetStatus.Lost)]
    [InlineData(AssetStatus.Disposed)]
    public void AssignTo_NotAvailable_ShouldThrowConflict(AssetStatus status)
    {
        var asset = NewAsset();
        asset.ChangeStatus(status);

        var act = () => asset.AssignTo(5, 1, Now, null, null);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("ASSET_NOT_AVAILABLE");
    }

    [Fact]
    public void AssignTo_AlreadyAssigned_ShouldThrowConflict()
    {
        var asset = NewAsset();
        asset.AssignTo(5, 1, Now, null, null);

        var act = () => asset.AssignTo(6, 1, Now, null, null);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("ASSET_NOT_AVAILABLE");
        asset.CurrentUserId.Should().Be(5);
    }

    [Fact]
    public void Return_ShouldCloseAssignmentWithoutLosingHistory()
    {
        var asset = NewAsset();
        var assignment = asset.AssignTo(5, 1, Now, "Nuevo", "Entrega");

        asset.Return(assignment, returnedById: 2, Now.AddDays(30), "Rayado", "Devuelto por cambio de puesto");

        asset.Status.Should().Be(AssetStatus.Available);
        asset.CurrentUserId.Should().BeNull();
        assignment.IsActive.Should().BeFalse();
        assignment.ReturnDate.Should().Be(Now.AddDays(30));
        assignment.ReturnedById.Should().Be(2);
        assignment.ConditionAtReturn.Should().Be("Rayado");
        assignment.ConditionAtAssignment.Should().Be("Nuevo", "los datos de la entrega no se sobrescriben");
        assignment.Notes.Should().Be("Entrega\nDevuelto por cambio de puesto");
    }

    [Theory]
    [InlineData(AssetStatus.Maintenance)]
    [InlineData(AssetStatus.Repair)]
    public void Return_WithFaultyCondition_CanLeaveAssetInMaintenanceOrRepair(AssetStatus resulting)
    {
        var asset = NewAsset();
        var assignment = asset.AssignTo(5, 1, Now, null, null);

        asset.Return(assignment, 2, Now, "Pantalla rota", null, resulting);

        asset.Status.Should().Be(resulting);
    }

    [Theory]
    [InlineData(AssetStatus.Retired)]
    [InlineData(AssetStatus.Disposed)]
    [InlineData(AssetStatus.Assigned)]
    public void Return_WithInvalidResultingStatus_ShouldThrow(AssetStatus resulting)
    {
        var asset = NewAsset();
        var assignment = asset.AssignTo(5, 1, Now, null, null);

        var act = () => asset.Return(assignment, 2, Now, null, null, resulting);

        act.Should().Throw<DomainValidationException>().Which.Code.Should().Be("INVALID_RETURN_STATUS");
        asset.Status.Should().Be(AssetStatus.Assigned);
        assignment.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Return_ClosedAssignment_ShouldThrowConflict()
    {
        var asset = NewAsset();
        var assignment = asset.AssignTo(5, 1, Now, null, null);
        asset.Return(assignment, 2, Now, null, null);

        var act = () => asset.Return(assignment, 2, Now, null, null);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("ASSET_NOT_ASSIGNED");
    }

    [Theory]
    [InlineData(AssetStatus.Available, AssetStatus.Maintenance, true)]
    [InlineData(AssetStatus.Available, AssetStatus.Disposed, true)]
    [InlineData(AssetStatus.Maintenance, AssetStatus.Available, true)]
    [InlineData(AssetStatus.Lost, AssetStatus.Available, true)]
    [InlineData(AssetStatus.Retired, AssetStatus.Repair, false)]
    [InlineData(AssetStatus.Disposed, AssetStatus.Available, false)]
    [InlineData(AssetStatus.Available, AssetStatus.Assigned, false)]
    [InlineData(AssetStatus.Assigned, AssetStatus.Available, false)]
    [InlineData(AssetStatus.Assigned, AssetStatus.Lost, false)]
    public void StatusRules_ShouldOnlyAllowDefinedTransitions(AssetStatus from, AssetStatus to, bool allowed)
    {
        AssetStatusRules.CanChange(from, to).Should().Be(allowed);
    }

    [Fact]
    public void ChangeStatus_WhileAssigned_ShouldThrowConflict()
    {
        var asset = NewAsset();
        asset.AssignTo(5, 1, Now, null, null);

        var act = () => asset.ChangeStatus(AssetStatus.Maintenance);

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public void EnsureCanBeDeleted_WhenAssigned_ShouldThrowConflict()
    {
        var asset = NewAsset();
        asset.AssignTo(5, 1, Now, null, null);

        var act = asset.EnsureCanBeDeleted;

        act.Should().Throw<ConflictException>().Which.Code.Should().Be("ASSET_ASSIGNED");
    }

    private static Asset NewAsset() => new() { AssetCode = "LT-001", Name = "Laptop", AssetTypeId = 1 };
}
