namespace TIAdmin.Tests.Unit;

using FluentAssertions;
using TIAdmin.Application.Common.Constants;
using Xunit;

public class PermissionsTests
{
    [Theory]
    [InlineData("Assets", "View", "ASSETS.VIEW")]
    [InlineData("AssetTypes", "Manage", "ASSET_TYPES.MANAGE")]
    [InlineData("TicketCategories", "Manage", "TICKET_CATEGORIES.MANAGE")]
    [InlineData("Maintenance", "View", "MAINTENANCE.VIEW")]
    public void PermissionCode_ShouldBeDerivedFromModuleAndAction(string module, string action, string expected)
    {
        var definition = new PermissionDefinition(module, action, "descripcion");

        definition.Code.Should().Be(expected);
    }

    [Fact]
    public void AllPermissions_ShouldUseUppercaseModuleDotActionFormat()
    {
        Permissions.All.Should().OnlyContain(p =>
            !p.Code.Any(char.IsLower)
            && p.Code.Contains('.')
            && !p.Code.EndsWith('.')
            && !p.Code.StartsWith('.'));
    }

    [Fact]
    public void AllPermissions_ShouldHaveUniqueCodes()
    {
        var codes = Permissions.All.Select(p => p.Code).ToList();

        codes.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(SystemRoles.SuperAdmin)]
    [InlineData(SystemRoles.TiAdmin)]
    [InlineData(SystemRoles.TiSupport)]
    [InlineData(SystemRoles.TiAssetManager)]
    [InlineData(SystemRoles.TiManager)]
    [InlineData(SystemRoles.Auditor)]
    [InlineData(SystemRoles.User)]
    public void ForRole_ShouldOnlyReturnExistingPermissions(string role)
    {
        var known = Permissions.All.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Permissions.ForRole(role).Should().OnlyContain(code => known.Contains(code));
    }

    [Fact]
    public void SuperAdmin_ShouldHaveEveryPermission()
    {
        Permissions.ForRole(SystemRoles.SuperAdmin)
            .Should().BeEquivalentTo(Permissions.All.Select(p => p.Code));
    }

    [Fact]
    public void UnknownRole_ShouldReturnEmptyPermissions()
    {
        Permissions.ForRole("NO_EXISTE").Should().BeEmpty();
    }

    [Fact]
    public void Auditor_ShouldBeReadOnly()
    {
        var auditorPermissions = Permissions.ForRole(SystemRoles.Auditor);

        auditorPermissions.Should().NotContain(new[]
        {
            Permissions.AssetsCreate, Permissions.AssetsUpdate, Permissions.AssetsDelete,
            Permissions.TicketsCreate, Permissions.TicketsUpdate, Permissions.TicketsClose,
            Permissions.UsersCreate, Permissions.UsersUpdate, Permissions.UsersDisable,
            Permissions.RolesManage, Permissions.ConfigurationManage
        });
    }

    [Fact]
    public void UserRole_ShouldOnlyBeAbleToCreateTicketsAndRequests()
    {
        Permissions.ForRole(SystemRoles.User).Should().BeEquivalentTo(new[]
        {
            Permissions.TicketsCreate,
            Permissions.RequestsCreate,
            Permissions.DashboardView,
            Permissions.NotificationsView
        });
    }

    [Fact]
    public void UserRole_ShouldNotSeeTheWholeInventory()
    {
        // Q-10: el usuario final solo ve sus activos asignados via GET /assets/mine.
        Permissions.ForRole(SystemRoles.User).Should().NotContain(Permissions.AssetsView);
    }

    [Fact]
    public void UserRole_ShouldOnlySeeOwnTickets()
    {
        // ADR-026: TICKETS.VIEW / REQUESTS.VIEW significan "ver todos"; el solicitante siempre ve los suyos.
        Permissions.ForRole(SystemRoles.User).Should().NotContain([Permissions.TicketsView, Permissions.RequestsView]);
    }
}

public class SystemRolesTests
{
    [Fact]
    public void All_ShouldContainTheSevenRolesFromSpecs()
    {
        SystemRoles.All.Should().BeEquivalentTo(new[]
        {
            "SUPER_ADMIN", "TI_ADMIN", "TI_SUPPORT", "TI_ASSET_MANAGER",
            "TI_MANAGER", "AUDITOR", "USER"
        });
    }

    [Fact]
    public void EveryRole_ShouldHaveDescription()
    {
        foreach (var role in SystemRoles.All)
        {
            SystemRoles.Descriptions.Should().ContainKey(role);
            SystemRoles.Descriptions[role].Should().NotBeNullOrWhiteSpace();
        }
    }
}
