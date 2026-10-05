namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using Xunit;

/// <summary>
/// Endpoints de departamentos y ubicaciones de extremo a extremo (HTTP → EF → interceptores).
/// </summary>
public sealed class OrganizationEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    [Fact]
    public async Task Department_CreateGetDelete_ShouldSoftDeleteAndAudit()
    {
        using var client = await factory.CreateAdminClientAsync();
        var code = UniqueCode("D");

        var created = await client.PostAsJsonAsync("/api/v1/departments", new { code, name = "Departamento" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var id = (await created.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentData>>())!.Data!.Id;

        (await client.GetAsync($"/api/v1/departments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/v1/departments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/departments/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var stored = await factory.WithDbContextAsync(db =>
            db.Departments.IgnoreQueryFilters().SingleAsync(d => d.Id == id));
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedBy.Should().NotBeNull();

        var actions = await factory.WithDbContextAsync(db => db.AuditLogs
            .Where(a => a.EntityName == nameof(Department) && a.EntityId == id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Select(a => a.Action)
            .ToListAsync());
        actions.Should().BeEquivalentTo([AuditAction.Create, AuditAction.Delete]);
    }

    [Fact]
    public async Task Department_ReusingCodeOfDeletedDepartment_ShouldReturn400()
    {
        using var client = await factory.CreateAdminClientAsync();
        var code = UniqueCode("R");
        var created = await client.PostAsJsonAsync("/api/v1/departments", new { code, name = "Original" });
        var id = (await created.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentData>>())!.Data!.Id;
        await client.DeleteAsync($"/api/v1/departments/{id}");

        var reuse = await client.PostAsJsonAsync("/api/v1/departments", new { code, name = "Reutilizado" });

        reuse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Department_Hierarchy_ShouldRejectUnknownParentCyclesAndDeletingParents()
    {
        using var client = await factory.CreateAdminClientAsync();
        var parentCode = UniqueCode("P");
        var parent = (await (await client.PostAsJsonAsync("/api/v1/departments", new { code = parentCode, name = "Padre" }))
            .Content.ReadFromJsonAsync<ApiEnvelope<DepartmentData>>())!.Data!;
        var child = (await (await client.PostAsJsonAsync("/api/v1/departments", new { code = UniqueCode("C"), name = "Hijo", parentId = parent.Id }))
            .Content.ReadFromJsonAsync<ApiEnvelope<DepartmentData>>())!.Data!;

        var unknownParent = await client.PostAsJsonAsync("/api/v1/departments", new { code = UniqueCode("X"), name = "X", parentId = 999_999 });
        var cycle = await client.PutAsJsonAsync($"/api/v1/departments/{parent.Id}",
            new { code = parentCode, name = "Padre", parentId = child.Id, isActive = true });
        var deleteParent = await client.DeleteAsync($"/api/v1/departments/{parent.Id}");

        unknownParent.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        cycle.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        deleteParent.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Department_InvalidPayload_ShouldReturn400ValidationErrors()
    {
        using var client = await factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/departments", new { code = "", name = new string('x', 151) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>();
        body!.Errors.Should().HaveCountGreaterThanOrEqualTo(2).And.OnlyContain(e => e.Code == "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Departments_List_ShouldSearchSortAndPage()
    {
        using var client = await factory.CreateAdminClientAsync();
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        foreach (var suffix in new[] { "A", "B", "C" })
        {
            await client.PostAsJsonAsync("/api/v1/departments", new { code = $"{tag}{suffix}", name = $"Lista {tag}" });
        }

        var response = await client.GetAsync($"/api/v1/departments?search={tag}&pageSize=2&page=1&sortBy=code&sortDirection=Descending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<ApiEnvelope<PagedData<DepartmentData>>>())!.Data!;
        page.TotalItems.Should().Be(3);
        page.TotalPages.Should().Be(2);
        page.Items.Select(d => d.Code).Should().Equal($"{tag}C", $"{tag}B");
    }

    [Fact]
    public async Task Location_CreateUpdateList_ShouldWork()
    {
        using var client = await factory.CreateAdminClientAsync();
        var code = UniqueCode("L");

        var created = await client.PostAsJsonAsync("/api/v1/locations", new { code, name = "Sede", city = "Managua" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<ApiEnvelope<LocationData>>())!.Data!.Id;

        var updated = await client.PutAsJsonAsync($"/api/v1/locations/{id}",
            new { code, name = "Sede Central", city = "Managua", isActive = false });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);

        var inactive = (await (await client.GetAsync($"/api/v1/locations?search={code}&isActive=false"))
            .Content.ReadFromJsonAsync<ApiEnvelope<PagedData<LocationData>>>())!.Data!;
        inactive.Items.Should().ContainSingle(l => l.Id == id && l.Name == "Sede Central" && !l.IsActive);
    }

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private sealed record DepartmentData(int Id, string Code, string Name, int? ParentId, bool IsActive);

    private sealed record LocationData(int Id, string Code, string Name, string? City, bool IsActive);
}
