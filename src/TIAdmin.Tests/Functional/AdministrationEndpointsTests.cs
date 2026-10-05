namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using Xunit;

public sealed class AdministrationEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task Audit_ShouldBeQueryableByAuditorsOnly()
    {
        using var admin = await factory.CreateAdminClientAsync();
        using var manager = await factory.CreateAssetManagerClientAsync();
        var department = await Post<IdData>(admin, "/api/v1/departments", new { code = $"AU{Guid.NewGuid().ToString("N")[..6]}", name = "Auditado" });

        var page = await Get<PagedData<AuditData>>(admin,
            $"/api/v1/audit?module=Organization&entityName=Department&entityId={department.Id}");
        var single = await Get<AuditData>(admin, $"/api/v1/audit/{page.Items[0].Id}");
        var forbidden = await manager.GetAsync("/api/v1/audit");
        var badRange = await admin.GetAsync("/api/v1/audit?from=2026-12-01&to=2026-01-01");

        page.Items.Should().ContainSingle(a => a.Action == AuditAction.Create && a.UserName == TIAdminApiFactory.AdminUserName);
        single.EntityId.Should().Be(department.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TI_ASSET_MANAGER no tiene AUDIT.VIEW");
        (await ErrorCode(badRange, HttpStatusCode.BadRequest)).Should().Be("INVALID_DATE_RANGE");
    }

    [Fact]
    public async Task Dashboard_ShouldOnlyIncludeSectionsTheUserMayView()
    {
        using var admin = await factory.CreateAdminClientAsync();
        using var manager = await factory.CreateAssetManagerClientAsync();
        var endUser = await CreateUserAsync(SystemRoles.User);
        var category = (await Get<List<CategoryData>>(endUser.Client, "/api/v1/ticket-categories")).First(c => c.Type == "Incident");
        await Post<IdData>(endUser.Client, "/api/v1/tickets", new { title = "Pantalla negra", description = "No enciende", categoryId = category.Id });

        var full = await Get<DashboardData>(admin, "/api/v1/dashboard/summary");
        var assetsOnly = await Get<DashboardData>(manager, "/api/v1/dashboard/summary");
        var mine = await Get<DashboardData>(endUser.Client, "/api/v1/dashboard/summary");

        full.Assets.Should().NotBeNull();
        full.Tickets.Should().NotBeNull();
        full.Tickets!.Open.Should().BeGreaterThanOrEqualTo(1);
        full.Licenses.Should().NotBeNull();
        full.Contracts.Should().NotBeNull();
        full.Maintenance.Should().NotBeNull();
        full.Costs.Should().NotBeNull();

        assetsOnly.Assets.Should().NotBeNull();
        assetsOnly.Tickets.Should().BeNull("TI_ASSET_MANAGER no tiene TICKETS.VIEW");
        assetsOnly.Costs.Should().NotBeNull("TI_ASSET_MANAGER tiene REPORTS.VIEW");

        mine.Mine.OpenTickets.Should().Be(1);
        mine.Assets.Should().BeNull();
        mine.Tickets.Should().BeNull();
        mine.Costs.Should().BeNull("el usuario final no ve costos de TI");
    }

    [Fact]
    public async Task Reports_ShouldAggregateAssetsTicketsSlaLicensesAndCosts()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var holder = await CreateUserAsync(SystemRoles.User);
        var asset = await Post<IdData>(admin, "/api/v1/assets", new
        {
            assetCode = $"RP{Guid.NewGuid().ToString("N")[..8]}".ToUpperInvariant(), name = "Laptop reporte", assetTypeId = 1, purchaseCost = 1000m
        });
        await Post<object>(admin, $"/api/v1/assets/{asset.Id}/assign", new { userId = holder.Id });

        var maintenance = await Post<IdData>(admin, "/api/v1/maintenances", new
        {
            title = "Correctivo", type = "Corrective", assetId = asset.Id, scheduledDate = DateTime.UtcNow
        });
        await Post<object>(admin, $"/api/v1/maintenances/{maintenance.Id}/start", new { });
        await Post<object>(admin, $"/api/v1/maintenances/{maintenance.Id}/complete", new { actions = "Cambio de disco", actualCost = 125m });

        var category = (await Get<List<CategoryData>>(admin, "/api/v1/ticket-categories")).First(c => c.Type == "Incident");
        var ticket = await Post<IdData>(admin, "/api/v1/tickets", new { title = "Lento", description = "Muy lento", categoryId = category.Id });
        await Post<object>(admin, $"/api/v1/tickets/{ticket.Id}/status", new { status = "InProgress" });
        await Post<object>(admin, $"/api/v1/tickets/{ticket.Id}/status", new { status = "Resolved", resolutionNotes = "Limpieza de temporales" });

        var assets = await Get<AssetSummaryData>(admin, "/api/v1/reports/assets/summary");
        var byUser = await Get<List<AssetsByUserData>>(admin, "/api/v1/reports/assets/by-user");
        var tickets = await Get<TicketReportData>(admin, "/api/v1/reports/tickets");
        var sla = await Get<SlaReportData>(admin, "/api/v1/reports/sla");
        var licenses = await admin.GetAsync("/api/v1/reports/licenses");
        var costs = await Get<CostReportData>(admin, "/api/v1/reports/costs");
        var badRange = await admin.GetAsync("/api/v1/reports/tickets?from=2026-10-10&to=2026-10-01");
        var endUserReport = await holder.Client.GetAsync("/api/v1/reports/tickets");

        assets.Total.Should().BeGreaterThanOrEqualTo(1);
        assets.TotalPurchaseCost.Should().BeGreaterThanOrEqualTo(1000m);
        assets.ByStatus.Should().Contain(s => s.Label == "Assigned");
        byUser.Should().ContainSingle(r => r.UserId == holder.Id).Which.AssetCount.Should().Be(1);
        tickets.Created.Should().BeGreaterThanOrEqualTo(1);
        tickets.Resolved.Should().BeGreaterThanOrEqualTo(1);
        tickets.AverageResolutionHours.Should().NotBeNull();
        sla.Resolved.Should().BeGreaterThanOrEqualTo(1);
        sla.CompliancePercent.Should().NotBeNull();
        licenses.StatusCode.Should().Be(HttpStatusCode.OK);
        costs.Maintenance.Should().BeGreaterThanOrEqualTo(125m);
        costs.ByMonth.Should().NotBeEmpty();
        (await ErrorCode(badRange, HttpStatusCode.BadRequest)).Should().Be("INVALID_DATE_RANGE");
        endUserReport.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Configuration_ShouldValidateValuesProtectRuntimeKeysAndTakeEffect()
    {
        using var admin = await factory.CreateAdminClientAsync();
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var anonymous = factory.CreateAnonymousClient();

        var all = await Get<List<ConfigData>>(admin, "/api/v1/configuration");
        var publicValues = await Get<Dictionary<string, string?>>(anonymous, "/api/v1/configuration/public");
        var validDays = await admin.PutAsJsonAsync("/api/v1/configuration/Alerts.Contract.Days", new { value = "60, 30, 7" });
        var invalidDays = await admin.PutAsJsonAsync("/api/v1/configuration/Alerts.Contract.Days", new { value = "60,abc" });
        var invalidInt = await admin.PutAsJsonAsync("/api/v1/configuration/Alerts.Maintenance.Days", new { value = "-3" });
        var runtimeKey = await admin.PutAsJsonAsync("/api/v1/configuration/App.TimeZone", new { value = "UTC" });
        var badPrefix = await admin.PutAsJsonAsync("/api/v1/configuration/Tickets.NumberPrefix", new { value = "TOOLONG7" });
        var unknown = await admin.PutAsJsonAsync("/api/v1/configuration/No.Existe", new { value = "x" });
        var managerUpdate = await manager.PutAsJsonAsync("/api/v1/configuration/Alerts.Contract.Days", new { value = "30" });

        all.Should().Contain(c => c.Key == "App.TimeZone" && c.IsRuntimeManaged && !c.IsEditable);
        publicValues.Should().ContainKey("App.TimeZone");
        validDays.StatusCode.Should().Be(HttpStatusCode.OK);
        (await validDays.Content.ReadFromJsonAsync<ApiEnvelope<ConfigData>>(TestJson.Options))!.Data!.Value.Should().Be("60,30,7");
        (await ErrorCode(invalidDays, HttpStatusCode.BadRequest)).Should().Be("INVALID_CONFIGURATION_VALUE");
        (await ErrorCode(invalidInt, HttpStatusCode.BadRequest)).Should().Be("INVALID_CONFIGURATION_VALUE");
        (await ErrorCode(runtimeKey, HttpStatusCode.Conflict)).Should().Be("CONFIGURATION_NOT_EDITABLE");
        (await ErrorCode(badPrefix, HttpStatusCode.BadRequest)).Should().Be("INVALID_CONFIGURATION_VALUE");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        managerUpdate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // El cambio surte efecto: el siguiente ticket usa el nuevo prefijo.
        (await admin.PutAsJsonAsync("/api/v1/configuration/Tickets.NumberPrefix", new { value = "INC" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var category = (await Get<List<CategoryData>>(admin, "/api/v1/ticket-categories")).First(c => c.Type == "Incident");
        var ticket = await Post<TicketNumberData>(admin, "/api/v1/tickets", new { title = "Prefijo", description = "Prueba", categoryId = category.Id });
        var reset = await Post<ConfigData>(admin, "/api/v1/configuration/Tickets.NumberPrefix/reset", new { });

        ticket.TicketNumber.Should().StartWith("INC-");
        reset.Value.Should().Be("TKT");
    }

    [Fact]
    public async Task EncryptedConfiguration_ShouldBeMaskedStoredEncryptedAndReadableBySettings()
    {
        using var admin = await factory.CreateAdminClientAsync();
        const string key = "Integrations.Smtp.Password";
        await factory.WithDbContextAsync(async db =>
        {
            if (!await db.SystemConfigurations.AnyAsync(c => c.Key == key))
            {
                db.SystemConfigurations.Add(new SystemConfiguration
                {
                    Key = key, Group = "Integrations", DataType = ConfigurationDataType.Encrypted, IsPublic = true, IsEditable = true,
                    Description = "Clave SMTP de prueba"
                });
            }

            return await db.SaveChangesAsync();
        });

        var updated = await admin.PutAsJsonAsync($"/api/v1/configuration/{key}", new { value = "S3cr3t!" });
        var body = await updated.Content.ReadAsStringAsync();
        var stored = await factory.WithDbContextAsync(db => db.SystemConfigurations.Where(c => c.Key == key).Select(c => c.Value).SingleAsync());
        var publicValues = await Get<Dictionary<string, string?>>(factory.CreateAnonymousClient(), "/api/v1/configuration/public");

        using var scope = factory.Services.CreateScope();
        var decrypted = await scope.ServiceProvider.GetRequiredService<ISystemSettings>().GetStringAsync(key, "");

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("********").And.NotContain("S3cr3t!");
        stored.Should().NotBeNullOrEmpty().And.NotBe("S3cr3t!");
        publicValues.Should().NotContainKey(key, "los parametros cifrados nunca son publicos");
        decrypted.Should().Be("S3cr3t!");
    }

    private async Task<TestUser> CreateUserAsync(string role)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var userName = $"ad{Guid.NewGuid().ToString("N")[..8]}";
        var user = await Post<IdData>(admin, "/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Ad", lastName = role
        });

        if (role != SystemRoles.User)
        {
            await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles", new { roles = new[] { role } });
        }

        return new TestUser(user.Id, await factory.LoginFreshAsync(userName, Password));
    }

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, TestJson.Options);
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

    private sealed record IdData(int Id);

    private sealed record CategoryData(int Id, string Code, string Type);

    private sealed record TicketNumberData(int Id, string TicketNumber);

    private sealed record AuditData(long Id, AuditAction Action, string Module, string EntityName, string EntityId, string? UserName);

    private sealed record LabelCount(string Label, int Count);

    private sealed record MineData(int OpenTickets, int AssignedAssets, int TicketsAssignedToMe);

    private sealed record TicketKpiData(int Open, int Overdue);

    private sealed record DashboardData(MineData Mine, object? Assets, TicketKpiData? Tickets, object? Licenses, object? Contracts, object? Maintenance, object? Costs);

    private sealed record AssetSummaryData(int Total, decimal TotalPurchaseCost, List<LabelCount> ByStatus);

    private sealed record AssetsByUserData(int UserId, int AssetCount);

    private sealed record TicketReportData(int Created, int Resolved, decimal? AverageResolutionHours);

    private sealed record SlaReportData(int Resolved, decimal? CompliancePercent);

    private sealed record CostMonth(int Year, int Month, decimal Total);

    private sealed record CostReportData(decimal Maintenance, decimal Total, List<CostMonth> ByMonth);

    private sealed record ConfigData(string Key, string? Value, bool IsEditable, bool IsRuntimeManaged);
}
