namespace TIAdmin.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Infrastructure.Persistence;
using TIAdmin.Infrastructure.Persistence.Seeding;
using TIAdmin.Tests.Functional;

[Trait("Category", "Integration")]
public sealed class SqlServerIntegrationTests(SqlServerApiFactory factory) : IClassFixture<SqlServerApiFactory>
{
    /// <summary>
    /// Todas las consultas de lectura deben traducirse a SQL: InMemory evalua en memoria y no detecta
    /// expresiones LINQ que SQL Server rechaza en tiempo de ejecucion.
    /// </summary>
    [DockerFact]
    public async Task ReadEndpoints_ShouldTranslateToSql()
    {
        using var admin = await factory.CreateAdminClientAsync();
        string[] urls =
        [
            "/api/v1/assets", "/api/v1/asset-types", "/api/v1/assignments", "/api/v1/departments", "/api/v1/locations",
            "/api/v1/users", "/api/v1/roles", "/api/v1/permissions", "/api/v1/vendors", "/api/v1/contracts", "/api/v1/software",
            "/api/v1/licenses", "/api/v1/tickets", "/api/v1/tickets?overdue=true", "/api/v1/ticket-categories", "/api/v1/sla-policies",
            "/api/v1/maintenances", "/api/v1/changes", "/api/v1/purchases", "/api/v1/alerts/licenses", "/api/v1/alerts/contracts", "/api/v1/alerts/maintenance", "/api/v1/audit", "/api/v1/configuration",
            "/api/v1/dashboard/summary", "/api/v1/notifications", "/api/v1/notifications/unread-count",
            "/api/v1/reports/assets/summary", "/api/v1/reports/assets/by-user", "/api/v1/reports/tickets", "/api/v1/reports/licenses", "/api/v1/reports/costs", "/api/v1/reports/sla",
            "/api/v1/reports/assets/export?format=xlsx", "/api/v1/reports/tickets/export?format=csv", "/api/v1/reports/audit/export?format=csv"
        ];

        var failures = new List<string>();
        foreach (var url in urls)
        {
            var response = await admin.GetAsync(url);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                failures.Add($"{url} -> {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            }
        }

        failures.Should().BeEmpty();
    }

    [DockerFact]
    public async Task Seeder_ShouldBeIdempotent()
    {
        var before = await factory.WithDbContextAsync(CountSeedRowsAsync);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        }

        (await factory.WithDbContextAsync(CountSeedRowsAsync)).Should().Be(before);
    }

    [DockerFact]
    public async Task UniqueIndexViolation_ShouldMapToDuplicateRecordConflict()
    {
        var code = Code();
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<TIAdminDbContext>();

        // Se salta la validacion del servicio para provocar la carrera que solo el indice detecta.
        context.Departments.Add(new Department { Code = code, Name = "Uno" });
        context.Departments.Add(new Department { Code = code, Name = "Dos" });
        var act = () => unitOfWork.SaveChangesAsync();

        (await act.Should().ThrowAsync<ConflictException>()).Which.Code.Should().Be("DUPLICATE_RECORD");
    }

    [DockerFact]
    public async Task FilteredUniqueIndex_ShouldIgnoreNullDedupKeys()
    {
        var adminId = await factory.WithDbContextAsync(db => db.Users.Where(u => u.UserName == TIAdminApiFactory.AdminUserName).Select(u => u.Id).SingleAsync());
        var key = $"it:{Guid.NewGuid():N}";
        Notification New(string? dedupKey) => new()
        {
            UserId = adminId, Type = "Test", Title = "t", Message = "m", DedupKey = dedupKey, CreatedAt = DateTime.UtcNow
        };

        await factory.WithDbContextAsync(async db =>
        {
            db.Notifications.AddRange(New(null), New(null), New(key));
            return await db.SaveChangesAsync();
        });
        var duplicate = () => factory.WithDbContextAsync(async db =>
        {
            db.Notifications.Add(New(key));
            return await db.SaveChangesAsync();
        });

        await duplicate.Should().ThrowAsync<DbUpdateException>("la clave de deduplicacion es unica por usuario");
    }

    [DockerFact]
    public async Task RowVersion_ShouldDetectConcurrentEdits()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var category = await PostAsync(admin, "/api/v1/ticket-categories", new
        {
            code = Code(), name = "Integracion", type = "Incident", defaultPriority = "Medium", requiresApproval = false, isActive = true
        });
        var ticket = await PostAsync(admin, "/api/v1/tickets", new
        {
            title = "Concurrencia", description = "Dos agentes editan a la vez", categoryId = category.Id
        });

        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<TIAdminDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<TIAdminDbContext>();
        var firstCopy = await first.Tickets.SingleAsync(t => t.Id == ticket.Id);
        var secondCopy = await second.Tickets.SingleAsync(t => t.Id == ticket.Id);

        firstCopy.Title = "Edicion del agente 1";
        await first.SaveChangesAsync();
        secondCopy.Title = "Edicion del agente 2";
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [DockerFact]
    public async Task Writes_ShouldProduceAuditTrailWithUserAndCorrelation()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var code = Code();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/departments") { Content = JsonContent.Create(new { code, name = "Auditado" }) };
        request.Headers.Add("X-Correlation-ID", $"it-{code}");

        var response = await admin.SendAsync(request);
        var id = (await response.Content.ReadFromJsonAsync<ApiEnvelope<IdData>>(TestJson.Options))!.Data!.Id;
        var audit = await factory.WithDbContextAsync(db => db.AuditLogs
            .Where(a => a.EntityName == nameof(Department) && a.EntityId == id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToListAsync());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        audit.Should().ContainSingle(a => a.Action == Domain.Enums.AuditAction.Create)
            .Which.Should().Match<AuditLog>(a => a.UserName == TIAdminApiFactory.AdminUserName && a.CorrelationId == $"it-{code}" && a.NewValues!.Contains(code));
    }

    [DockerFact]
    public async Task ReadinessProbe_ShouldBeHealthy()
    {
        using var client = factory.CreateAnonymousClient();

        var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain("\"status\":\"Healthy\"").And.Contain("\"name\":\"storage\"");
        body.Should().NotContainAny("Password", "Server=", "exception");
    }

    private static async Task<int> CountSeedRowsAsync(TIAdminDbContext db) =>
        await db.Permissions.CountAsync() + await db.Roles.CountAsync() + await db.SystemConfigurations.CountAsync()
        + await db.AssetTypes.CountAsync() + await db.Users.CountAsync();

    private static async Task<IdData> PostAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<IdData>>(TestJson.Options))!.Data!;
    }

    private static string Code() => $"IT{Guid.NewGuid().ToString("N")[..8]}".ToUpperInvariant();

    private sealed record IdData(int Id);
}
