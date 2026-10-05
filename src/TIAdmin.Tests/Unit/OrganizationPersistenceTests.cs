namespace TIAdmin.Tests.Unit;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Infrastructure.Persistence;
using TIAdmin.Infrastructure.Persistence.Interceptors;
using TIAdmin.Infrastructure.Persistence.Repositories;
using Xunit;

/// <summary>
/// Verifica las convenciones de persistencia (soft delete + auditoria) y los repositorios
/// de organizacion sobre EF Core InMemory con los interceptores reales.
/// </summary>
public sealed class OrganizationPersistenceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly TIAdminDbContext context;

    public OrganizationPersistenceTests()
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(7);
        currentUser.UserName.Returns("tester");

        var auditContext = Substitute.For<IAuditContext>();

        var options = new DbContextOptionsBuilder<TIAdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(
                new AuditSaveChangesInterceptor(currentUser, clock),
                new AuditTrailInterceptor(currentUser, clock, auditContext))
            .Options;

        context = new TIAdminDbContext(options);
    }

    public void Dispose() => context.Dispose();

    [Fact]
    public async Task Delete_OnSoftDeletableEntity_ShouldMarkAsDeletedInsteadOfRemoving()
    {
        var department = await AddDepartmentAsync("TI", "Tecnologia");
        var repository = new DepartmentRepository(context);

        repository.Delete(department);
        await context.SaveChangesAsync();

        var stored = await context.Departments.IgnoreQueryFilters().SingleAsync(d => d.Id == department.Id);
        stored.IsDeleted.Should().BeTrue();
        stored.DeletedAt.Should().Be(Now);
        stored.DeletedBy.Should().Be(7);
        (await context.Departments.AnyAsync(d => d.Id == department.Id)).Should().BeFalse("el query filter oculta los eliminados");
    }

    [Fact]
    public async Task Delete_OnSoftDeletableEntity_ShouldWriteDeleteAuditLog()
    {
        var department = await AddDepartmentAsync("TI", "Tecnologia");

        new DepartmentRepository(context).Delete(department);
        await context.SaveChangesAsync();

        var log = await context.AuditLogs.SingleAsync(a => a.EntityName == nameof(Department) && a.Action == AuditAction.Delete);
        log.EntityId.Should().Be(department.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        log.UserId.Should().Be(7);
        log.NewValues.Should().BeNull();
    }

    [Fact]
    public async Task Create_ShouldWriteSingleCreateAuditLogWithPersistedId()
    {
        var department = await AddDepartmentAsync("TI", "Tecnologia");

        var log = await context.AuditLogs.SingleAsync(a => a.EntityName == nameof(Department));
        log.Action.Should().Be(AuditAction.Create);
        log.EntityId.Should().Be(department.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        log.NewValues.Should().Contain("\"Code\":\"TI\"");
        context.ChangeTracker.HasChanges().Should().BeFalse("el AuditLog de alta se guarda en el mismo SaveChanges");
    }

    [Fact]
    public async Task ExistsCodeAsync_ShouldConsiderSoftDeletedRows()
    {
        var department = await AddDepartmentAsync("TI", "Tecnologia");
        var repository = new DepartmentRepository(context);
        repository.Delete(department);
        await context.SaveChangesAsync();

        (await repository.ExistsCodeAsync("TI")).Should().BeTrue();
        (await repository.ExistsCodeAsync("TI", excludeId: department.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task WouldCreateCycleAsync_ShouldDetectSelfAndDescendants()
    {
        var root = await AddDepartmentAsync("ROOT", "Raiz");
        var child = await AddDepartmentAsync("CHILD", "Hijo", root.Id);
        var grandChild = await AddDepartmentAsync("GRAND", "Nieto", child.Id);
        var other = await AddDepartmentAsync("OTHER", "Otro");
        var repository = new DepartmentRepository(context);

        (await repository.WouldCreateCycleAsync(root.Id, root.Id)).Should().BeTrue();
        (await repository.WouldCreateCycleAsync(root.Id, grandChild.Id)).Should().BeTrue();
        (await repository.WouldCreateCycleAsync(grandChild.Id, other.Id)).Should().BeFalse();
        (await repository.WouldCreateCycleAsync(other.Id, child.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterSortAndPage()
    {
        await AddDepartmentAsync("FIN", "Finanzas");
        await AddDepartmentAsync("TI", "Tecnologia");
        await AddDepartmentAsync("TI-SOP", "Soporte TI");
        var inactive = await AddDepartmentAsync("TI-OLD", "TI Legado");
        inactive.IsActive = false;
        await context.SaveChangesAsync();

        var repository = new DepartmentRepository(context);
        var query = new PagedQuery { Search = "TI", PageSize = 1, SortBy = "code", SortDirection = SortDirection.Descending };

        var page = await repository.SearchAsync(query, isActive: true, parentId: null);

        page.TotalItems.Should().Be(2);
        page.TotalPages.Should().Be(2);
        page.Items.Should().ContainSingle().Which.Code.Should().Be("TI-SOP");
    }

    private async Task<Department> AddDepartmentAsync(string code, string name, int? parentId = null)
    {
        var department = new Department { Code = code, Name = name, ParentId = parentId };
        context.Departments.Add(department);
        await context.SaveChangesAsync();
        return department;
    }
}
