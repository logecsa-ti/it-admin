namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TIAdmin.Application.Common.Constants;
using Xunit;

public sealed class RolesEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task GetAll_ShouldListSystemRolesWithCounts()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var roles = await GetDataAsync<List<RoleData>>(admin, "/api/v1/roles");

        roles.Where(r => r.IsSystemRole).Select(r => r.Name).Should().BeEquivalentTo(SystemRoles.All);
        roles.Single(r => r.Name == SystemRoles.SuperAdmin).PermissionCount.Should().Be(Permissions.All.Count);
        roles.Single(r => r.Name == SystemRoles.SuperAdmin).UserCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PermissionCatalog_ShouldMatchCodeCatalog()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var catalog = await GetDataAsync<List<PermissionData>>(admin, "/api/v1/permissions");

        catalog.Select(p => p.Code).Should().BeEquivalentTo(Permissions.All.Select(p => p.Code));
    }

    [Fact]
    public async Task CustomRole_ShouldGrantAndRevokeAccessDynamically()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var roleName = UniqueRole();

        var created = await admin.PostAsJsonAsync("/api/v1/roles", new
        {
            name = roleName,
            description = "Solo lectura de organizacion",
            permissions = new[] { Permissions.OrganizationView }
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var role = (await created.Content.ReadFromJsonAsync<ApiEnvelope<RoleDetailData>>())!.Data!;
        role.IsSystemRole.Should().BeFalse();
        role.Permissions.Should().Equal(Permissions.OrganizationView);

        // Usuario sin permisos de organizacion → se le asigna solo el rol nuevo.
        var userName = $"rbac{Guid.NewGuid().ToString("N")[..8]}";
        var user = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Rbac", lastName = "Dinamico"
        });
        var userId = (await user.Content.ReadFromJsonAsync<ApiEnvelope<IdData>>())!.Data!.Id;
        (await admin.PutAsJsonAsync($"/api/v1/users/{userId}/roles", new { roles = new[] { roleName } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using (var withAccess = await factory.LoginFreshAsync(userName, Password))
        {
            (await withAccess.GetAsync("/api/v1/departments")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await withAccess.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        (await admin.PutAsJsonAsync($"/api/v1/roles/{role.Id}/permissions", new { permissions = new[] { Permissions.UsersView } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var afterChange = await factory.LoginFreshAsync(userName, Password);
        (await afterChange.GetAsync("/api/v1/departments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await afterChange.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithDuplicateOrInvalidNameOrUnknownPermission_ShouldFail()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var duplicate = await admin.PostAsJsonAsync("/api/v1/roles", new { name = SystemRoles.Auditor, description = "x" });
        var invalidName = await admin.PostAsJsonAsync("/api/v1/roles", new { name = "rol con espacios" });
        var unknownPermission = await admin.PostAsJsonAsync("/api/v1/roles",
            new { name = UniqueRole(), permissions = new[] { "NO.EXISTE" } });

        (await ErrorCodeAsync(duplicate, HttpStatusCode.Conflict)).Should().Be("ROLE_ALREADY_EXISTS");
        (await ErrorCodeAsync(invalidName, HttpStatusCode.BadRequest)).Should().Be("VALIDATION_ERROR");
        (await ErrorCodeAsync(unknownPermission, HttpStatusCode.BadRequest)).Should().Be("PERMISSION_NOT_FOUND");
    }

    [Fact]
    public async Task Create_WithoutRolesManage_ShouldReturn403()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();

        var response = await manager.PostAsJsonAsync("/api/v1/roles", new { name = UniqueRole() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SystemRole_CannotBeRenamedButDescriptionCanChange()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var auditorId = await RoleIdAsync(admin, SystemRoles.Auditor);

        var rename = await admin.PutAsJsonAsync($"/api/v1/roles/{auditorId}", new { name = "AUDITOR_X", description = "x" });
        var describe = await admin.PutAsJsonAsync($"/api/v1/roles/{auditorId}",
            new { name = SystemRoles.Auditor, description = "Auditoria interna" });

        (await ErrorCodeAsync(rename, HttpStatusCode.BadRequest)).Should().Be("SYSTEM_ROLE_RENAME");
        describe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await describe.Content.ReadFromJsonAsync<ApiEnvelope<RoleDetailData>>())!.Data!.Description.Should().Be("Auditoria interna");
    }

    [Fact]
    public async Task CustomRole_CanBeRenamed()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var role = await CreateRoleAsync(admin, UniqueRole());
        var newName = UniqueRole();

        var response = await admin.PutAsJsonAsync($"/api/v1/roles/{role.Id}", new { name = newName, description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ApiEnvelope<RoleDetailData>>())!.Data!.Name.Should().Be(newName);
    }

    [Fact]
    public async Task SuperAdminPermissions_ShouldBeImmutable()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var superAdminId = await RoleIdAsync(admin, SystemRoles.SuperAdmin);

        var response = await admin.PutAsJsonAsync($"/api/v1/roles/{superAdminId}/permissions",
            new { permissions = new[] { Permissions.DashboardView } });

        (await ErrorCodeAsync(response, HttpStatusCode.BadRequest)).Should().Be("SUPER_ADMIN_IMMUTABLE");
    }

    [Fact]
    public async Task Delete_ShouldRejectSystemRolesAndRolesInUse_AndRemoveUnusedCustomRoles()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var systemRole = await RoleIdAsync(admin, SystemRoles.Auditor);
        var unused = await CreateRoleAsync(admin, UniqueRole());
        var inUse = await CreateRoleAsync(admin, UniqueRole());
        var userName = $"del{Guid.NewGuid().ToString("N")[..8]}";
        var user = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Del", lastName = "Rol"
        });
        var userId = (await user.Content.ReadFromJsonAsync<ApiEnvelope<IdData>>())!.Data!.Id;
        await admin.PutAsJsonAsync($"/api/v1/users/{userId}/roles", new { roles = new[] { inUse.Name } });

        (await ErrorCodeAsync(await admin.DeleteAsync($"/api/v1/roles/{systemRole}"), HttpStatusCode.Conflict))
            .Should().Be("SYSTEM_ROLE_DELETE");
        (await ErrorCodeAsync(await admin.DeleteAsync($"/api/v1/roles/{inUse.Id}"), HttpStatusCode.Conflict))
            .Should().Be("ROLE_IN_USE");
        (await admin.DeleteAsync($"/api/v1/roles/{unused.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/roles/{unused.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<RoleDetailData> CreateRoleAsync(HttpClient admin, string name)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/roles",
            new { name, description = "Rol de prueba", permissions = new[] { Permissions.DashboardView } });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<RoleDetailData>>())!.Data!;
    }

    private static async Task<int> RoleIdAsync(HttpClient admin, string name) =>
        (await GetDataAsync<List<RoleData>>(admin, "/api/v1/roles")).Single(r => r.Name == name).Id;

    private static async Task<T> GetDataAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>())!.Data!;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        return body!.Errors!.First().Code;
    }

    private static string UniqueRole() => $"ROL_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private sealed record RoleData(int Id, string Name, bool IsSystemRole, int UserCount, int PermissionCount);

    private sealed record RoleDetailData(int Id, string Name, string? Description, bool IsSystemRole, IReadOnlyList<string> Permissions);

    private sealed record PermissionData(string Code, string Module, string Action);

    private sealed record IdData(int Id);
}
