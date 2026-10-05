namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Domain.Enums;
using Xunit;

public sealed class UsersEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task Create_ShouldReturn201WithDefaultUserRoleAndAuditWithoutSecrets()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var userName = Unique("ana");

        var response = await admin.PostAsJsonAsync("/api/v1/users", NewUser(userName));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = (await response.Content.ReadFromJsonAsync<ApiEnvelope<UserData>>())!.Data!;
        user.UserName.Should().Be(userName);
        user.FullName.Should().Be("Ana Prueba");
        user.IsActive.Should().BeTrue();
        user.Roles.Should().Equal(SystemRoles.User);

        var audit = await factory.WithDbContextAsync(db => db.AuditLogs
            .SingleAsync(a => a.EntityName == "User" && a.EntityId == user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        audit.Action.Should().Be(AuditAction.Create);
        audit.Module.Should().Be("Users");
        audit.NewValues.Should().NotContain("PasswordHash").And.NotContain("SecurityStamp");
    }

    [Fact]
    public async Task Create_WithDuplicates_ShouldReturn409()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var userName = Unique("dup");
        var employeeCode = Unique("EMP");
        await admin.PostAsJsonAsync("/api/v1/users", NewUser(userName, employeeCode: employeeCode));

        var sameUserName = await admin.PostAsJsonAsync("/api/v1/users", NewUser(userName, email: $"{Unique("x")}@tiadmin.tests"));
        var sameEmail = await admin.PostAsJsonAsync("/api/v1/users", NewUser(Unique("other"), email: $"{userName}@tiadmin.tests"));
        var sameEmployeeCode = await admin.PostAsJsonAsync("/api/v1/users", NewUser(Unique("emp"), employeeCode: employeeCode));

        (await ErrorCodeAsync(sameUserName, HttpStatusCode.Conflict)).Should().Be("USERNAME_ALREADY_EXISTS");
        (await ErrorCodeAsync(sameEmail, HttpStatusCode.Conflict)).Should().Be("EMAIL_ALREADY_EXISTS");
        (await ErrorCodeAsync(sameEmployeeCode, HttpStatusCode.Conflict)).Should().Be("EMPLOYEE_CODE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Create_WithWeakPasswordOrUnknownDepartment_ShouldReturn400()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var weak = await admin.PostAsJsonAsync("/api/v1/users", NewUser(Unique("weak"), password: "corta"));
        var unknownDepartment = await admin.PostAsJsonAsync("/api/v1/users", NewUser(Unique("dep"), departmentId: 999_999));

        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var weakBody = await weak.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        weakBody!.Errors.Should().Contain(e => e.Message.Contains("al menos 10 caracteres"));
        (await ErrorCodeAsync(unknownDepartment, HttpStatusCode.BadRequest)).Should().Be("DEPARTMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Create_WithoutUsersCreatePermission_ShouldReturn403()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();

        var response = await manager.PostAsJsonAsync("/api/v1/users", NewUser(Unique("nope")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_ShouldChangeProfileAndDepartment()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, Unique("upd"));
        var department = await CreateDepartmentAsync(admin);

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}", new
        {
            email = $"{user.UserName}.new@tiadmin.tests",
            firstName = "Ana Maria",
            lastName = "Actualizada",
            employeeCode = Unique("E"),
            jobTitle = "Analista",
            departmentId = department,
            locationId = (int?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<ApiEnvelope<UserData>>())!.Data!;
        updated.FullName.Should().Be("Ana Maria Actualizada");
        updated.DepartmentId.Should().Be(department);
        updated.Email.Should().EndWith(".new@tiadmin.tests");
    }

    [Fact]
    public async Task List_ShouldFilterByDepartmentRoleAndSearch()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var department = await CreateDepartmentAsync(admin);
        var tag = Unique("lst");
        var inDepartment = await CreateUserAsync(admin, $"{tag}.a", departmentId: department);
        await CreateUserAsync(admin, $"{tag}.b");

        var byDepartment = await GetPageAsync(admin, $"/api/v1/users?departmentId={department}");
        var bySearch = await GetPageAsync(admin, $"/api/v1/users?search={tag}&sortBy=username&sortDirection=Descending");
        var byRole = await GetPageAsync(admin, $"/api/v1/users?role={SystemRoles.SuperAdmin}");

        byDepartment.Items.Should().ContainSingle().Which.Id.Should().Be(inDepartment.Id);
        bySearch.Items.Select(u => u.UserName).Should().Equal($"{tag}.b", $"{tag}.a");
        byRole.Items.Should().ContainSingle().Which.UserName.Should().Be(TIAdminApiFactory.AdminUserName);
    }

    [Fact]
    public async Task Deactivate_ShouldBlockLoginAndRevokeRefreshTokens_ActivateShouldRestore()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, Unique("off"));
        using var anonymous = factory.CreateAnonymousClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { userName = user.UserName, password = Password });
        var refreshToken = (await login.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>())!.Data!.RefreshToken;

        (await admin.PostAsync($"/api/v1/users/{user.Id}/deactivate", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { userName = user.UserName, password = Password }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await admin.PostAsync($"/api/v1/users/{user.Id}/activate", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { userName = user.UserName, password = Password }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_Self_ShouldReturn400()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var adminId = await factory.WithDbContextAsync(db =>
            db.Users.Where(u => u.UserName == TIAdminApiFactory.AdminUserName).Select(u => u.Id).SingleAsync());

        var response = await admin.PostAsync($"/api/v1/users/{adminId}/deactivate", null);

        (await ErrorCodeAsync(response, HttpStatusCode.BadRequest)).Should().Be("CANNOT_DEACTIVATE_SELF");
    }

    [Fact]
    public async Task SetRoles_ShouldGrantPermissionsOnNextLoginAndBeAudited()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, Unique("sup"));

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles",
            new { roles = new[] { SystemRoles.TiSupport, SystemRoles.User } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ApiEnvelope<UserData>>())!.Data!.Roles
            .Should().BeEquivalentTo([SystemRoles.TiSupport, SystemRoles.User]);

        using var client = await factory.LoginFreshAsync(user.UserName, Password);
        var me = (await (await client.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<ApiEnvelope<ProfileData>>())!.Data!;
        me.Permissions.Should().Contain(Permissions.TicketsView);

        var roleAudits = await factory.WithDbContextAsync(db => db.AuditLogs
            .Where(a => a.EntityName == "UserRole" && a.EntityId.StartsWith($"UserId={user.Id};"))
            .ToListAsync());
        roleAudits.Should().Contain(a => a.Action == AuditAction.Create && a.Module == "Users");
    }

    [Fact]
    public async Task SetRoles_WithoutRolesManage_OrUnknownRole_ShouldFail()
    {
        using var admin = await factory.CreateAdminClientAsync();
        using var manager = await factory.CreateAssetManagerClientAsync();
        var user = await CreateUserAsync(admin, Unique("rol"));

        var forbidden = await manager.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles", new { roles = new[] { SystemRoles.SuperAdmin } });
        var unknown = await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles", new { roles = new[] { "NO_EXISTE" } });

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCodeAsync(unknown, HttpStatusCode.BadRequest)).Should().Be("ROLE_NOT_FOUND");
    }

    [Fact]
    public async Task SetRoles_RemovingLastSuperAdmin_ShouldReturn409()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var adminId = await factory.WithDbContextAsync(db =>
            db.Users.Where(u => u.UserName == TIAdminApiFactory.AdminUserName).Select(u => u.Id).SingleAsync());

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{adminId}/roles", new { roles = new[] { SystemRoles.TiAdmin } });

        (await ErrorCodeAsync(response, HttpStatusCode.Conflict)).Should().Be("LAST_SUPER_ADMIN");
    }

    [Fact]
    public async Task SetDirectPermissions_ShouldAddToEffectivePermissions()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, Unique("dir"));

        var response = await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/permissions",
            new { permissions = new[] { Permissions.ReportsView } });
        var unknown = await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/permissions",
            new { permissions = new[] { "NO.EXISTE" } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ApiEnvelope<UserData>>())!.Data!.DirectPermissions
            .Should().Equal(Permissions.ReportsView);
        (await ErrorCodeAsync(unknown, HttpStatusCode.BadRequest)).Should().Be("PERMISSION_NOT_FOUND");

        using var client = await factory.LoginFreshAsync(user.UserName, Password);
        var me = (await (await client.GetAsync("/api/v1/auth/me")).Content.ReadFromJsonAsync<ApiEnvelope<ProfileData>>())!.Data!;
        me.Permissions.Should().Contain(Permissions.ReportsView);
    }

    [Fact]
    public async Task Login_ShouldNotWriteUserUpdateAudit()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var user = await CreateUserAsync(admin, Unique("noise"));
        var userId = user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        using var _ = await factory.LoginFreshAsync(user.UserName, Password);

        var updates = await factory.WithDbContextAsync(db => db.AuditLogs
            .CountAsync(a => a.EntityName == "User" && a.EntityId == userId && a.Action == AuditAction.Update));
        updates.Should().Be(0, "LastLoginAt y ConcurrencyStamp son ruido, no cambios administrativos");
    }

    [Fact]
    public async Task GetById_Unknown_ShouldReturn404()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var response = await admin.GetAsync("/api/v1/users/999999");

        (await ErrorCodeAsync(response, HttpStatusCode.NotFound)).Should().Be("USER_NOT_FOUND");
    }

    private static object NewUser(
        string userName,
        string? email = null,
        string password = Password,
        string? employeeCode = null,
        int? departmentId = null) => new
    {
        userName,
        email = email ?? $"{userName}@tiadmin.tests",
        password,
        firstName = "Ana",
        lastName = "Prueba",
        employeeCode,
        jobTitle = (string?)null,
        departmentId,
        locationId = (int?)null
    };

    private static async Task<UserData> CreateUserAsync(HttpClient admin, string userName, int? departmentId = null)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/users", NewUser(userName, departmentId: departmentId));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<UserData>>())!.Data!;
    }

    private static async Task<int> CreateDepartmentAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/departments", new { code = Unique("D"), name = "Depto usuarios" });
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<IdData>>())!.Data!.Id;
    }

    private static async Task<PagedData<UserData>> GetPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<PagedData<UserData>>>())!.Data!;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        return body!.Errors!.First().Code;
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid().ToString("N")[..8]}";

    private sealed record UserData(
        int Id,
        string UserName,
        string Email,
        string FullName,
        int? DepartmentId,
        bool IsActive,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string>? DirectPermissions);

    private sealed record IdData(int Id);

    private sealed record ProfileData(IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> Permissions);
}
