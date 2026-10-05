namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Domain.Enums;
using Xunit;

public sealed class TicketsEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task Create_ByEndUser_ShouldNumberTicketApplySlaAndIgnoreRequestedPriority()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        var printing = await CategoryAsync("PRINTING");

        var ticket = await Post<TicketData>(requester.Client, "/api/v1/tickets",
            NewTicket(printing.Id, priority: "Critical"));

        ticket.TicketNumber.Should().MatchRegex(@"^TKT-\d{4}-\d{6}$");
        ticket.Type.Should().Be(TicketType.Incident);
        ticket.Status.Should().Be(TicketStatus.New);
        ticket.Priority.Should().Be(printing.DefaultPriority, "solo un agente fija la prioridad");
        ticket.RequesterId.Should().Be(requester.Id);
        ticket.Sla.DueAtResponse.Should().NotBeNull();
        ticket.Sla.DueAtResolution.Should().BeAfter(ticket.Sla.DueAtResponse!.Value);
        ticket.Sla.IsSlaBreached.Should().BeFalse();

        var history = await Get<List<HistoryData>>(requester.Client, $"/api/v1/tickets/{ticket.Id}/history");
        history.Should().ContainSingle().Which.ToStatus.Should().Be(TicketStatus.New);
    }

    [Fact]
    public async Task Visibility_EndUsersSeeOnlyTheirTicketsAndNeverInternalComments()
    {
        var alice = await CreateUserAsync(SystemRoles.User);
        var bob = await CreateUserAsync(SystemRoles.User);
        var agent = await CreateUserAsync(SystemRoles.TiSupport);
        var manager = await CreateUserAsync(SystemRoles.TiManager);
        var category = await CategoryAsync("HARDWARE");
        var aliceTicket = await Post<TicketData>(alice.Client, "/api/v1/tickets", NewTicket(category.Id));
        var bobTicket = await Post<TicketData>(bob.Client, "/api/v1/tickets", NewTicket(category.Id));

        await Post<List<CommentData>>(agent.Client, $"/api/v1/tickets/{aliceTicket.Id}/comments",
            new { content = "Nota interna: posible falla de disco", isInternal = true });
        await Post<List<CommentData>>(agent.Client, $"/api/v1/tickets/{aliceTicket.Id}/comments",
            new { content = "Vamos a revisar su equipo", isInternal = false });

        var aliceList = await Get<PagedData<TicketData>>(alice.Client, "/api/v1/tickets?pageSize=200");
        var bobTicketForAlice = await alice.Client.GetAsync($"/api/v1/tickets/{bobTicket.Id}");
        var agentList = await Get<PagedData<TicketData>>(agent.Client, "/api/v1/tickets?pageSize=200");
        var managerList = await Get<PagedData<TicketData>>(manager.Client, "/api/v1/tickets?pageSize=200");
        var aliceComments = await Get<List<CommentData>>(alice.Client, $"/api/v1/tickets/{aliceTicket.Id}/comments");
        var managerComments = await Get<List<CommentData>>(manager.Client, $"/api/v1/tickets/{aliceTicket.Id}/comments");

        aliceList.Items.Should().OnlyContain(t => t.RequesterId == alice.Id).And.Contain(t => t.Id == aliceTicket.Id);
        bobTicketForAlice.StatusCode.Should().Be(HttpStatusCode.NotFound, "no revela la existencia de tickets ajenos");
        agentList.Items.Select(t => t.Id).Should().Contain([aliceTicket.Id, bobTicket.Id]);
        managerList.Items.Select(t => t.Id).Should().Contain([aliceTicket.Id, bobTicket.Id]);
        aliceComments.Should().ContainSingle().Which.IsInternal.Should().BeFalse();
        managerComments.Should().HaveCount(2);
    }

    [Fact]
    public async Task Workflow_ShouldFollowStatesAndLetRequesterReactivateAndConfirm()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        var agent = await CreateUserAsync(SystemRoles.TiSupport);
        using var admin = await factory.CreateAdminClientAsync();
        var ticket = await Post<TicketData>(requester.Client, "/api/v1/tickets", NewTicket((await CategoryAsync("SOFTWARE")).Id));

        var assigned = await Post<TicketData>(admin, $"/api/v1/tickets/{ticket.Id}/assign", new { assignedToId = agent.Id });
        await Post<TicketData>(agent.Client, $"/api/v1/tickets/{ticket.Id}/status", new { status = "InProgress" });
        await Post<TicketData>(agent.Client, $"/api/v1/tickets/{ticket.Id}/status", new { status = "WaitingUser", comment = "Necesito captura" });
        await Post<List<CommentData>>(requester.Client, $"/api/v1/tickets/{ticket.Id}/comments", new { content = "Adjunto captura", isInternal = false });
        var reactivated = await Get<TicketData>(agent.Client, $"/api/v1/tickets/{ticket.Id}");

        var resolveWithoutNotes = await agent.Client.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/status", new { status = "Resolved" });
        var requesterResolve = await requester.Client.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/status",
            new { status = "Resolved", resolutionNotes = "Lo arregle yo" });
        var resolved = await Post<TicketData>(agent.Client, $"/api/v1/tickets/{ticket.Id}/status",
            new { status = "Resolved", resolutionNotes = "Se reinstalo la aplicacion" });
        var closed = await Post<TicketData>(requester.Client, $"/api/v1/tickets/{ticket.Id}/status", new { status = "Closed" });
        var history = await Get<List<HistoryData>>(requester.Client, $"/api/v1/tickets/{ticket.Id}/history");

        assigned.Status.Should().Be(TicketStatus.Open);
        assigned.AssignedToId.Should().Be(agent.Id);
        assigned.Sla.FirstResponseAt.Should().NotBeNull();
        reactivated.Status.Should().Be(TicketStatus.InProgress, "la respuesta del solicitante reactiva el ticket");
        (await ErrorCode(resolveWithoutNotes, HttpStatusCode.BadRequest)).Should().Be("VALIDATION_ERROR");
        requesterResolve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        resolved.ResolutionNotes.Should().Be("Se reinstalo la aplicacion");
        closed.Status.Should().Be(TicketStatus.Closed);
        history.Select(h => h.ToStatus).Should().Equal(
            TicketStatus.New, TicketStatus.Open, TicketStatus.InProgress, TicketStatus.WaitingUser,
            TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed);
    }

    [Fact]
    public async Task Permissions_ShouldRestrictActionsByRole()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        var otherUser = await CreateUserAsync(SystemRoles.User);
        var manager = await CreateUserAsync(SystemRoles.TiManager);
        using var admin = await factory.CreateAdminClientAsync();
        using var assetManager = await factory.CreateAssetManagerClientAsync();
        var category = await CategoryAsync("NETWORK");
        var ticket = await Post<TicketData>(requester.Client, "/api/v1/tickets", NewTicket(category.Id));

        var managerComment = await manager.Client.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/comments",
            new { content = "Opino", isInternal = false });
        var internalByRequester = await requester.Client.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/comments",
            new { content = "Secreto", isInternal = true });
        var assignToEndUser = await admin.PostAsJsonAsync($"/api/v1/tickets/{ticket.Id}/assign", new { assignedToId = otherUser.Id });
        var noCreatePermission = await assetManager.PostAsJsonAsync("/api/v1/tickets", NewTicket(category.Id));
        var cancelled = await Post<TicketData>(requester.Client, $"/api/v1/tickets/{ticket.Id}/status",
            new { status = "Cancelled", comment = "Ya funciona" });

        managerComment.StatusCode.Should().Be(HttpStatusCode.Forbidden, "supervisores ven pero no intervienen");
        internalByRequester.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCode(assignToEndUser, HttpStatusCode.BadRequest)).Should().Be("ASSIGNEE_NOT_AGENT");
        noCreatePermission.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        cancelled.Status.Should().Be(TicketStatus.Cancelled);
    }

    [Fact]
    public async Task ServiceRequest_WithApproval_ShouldWaitForApprovalBeforeWorkAndSla()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        var agent = await CreateUserAsync(SystemRoles.TiSupport);
        var manager = await CreateUserAsync(SystemRoles.TiManager);
        using var admin = await factory.CreateAdminClientAsync();
        var equipment = await CategoryAsync("REQ_EQUIPMENT");

        var request = await Post<TicketData>(requester.Client, "/api/v1/tickets", NewTicket(equipment.Id) with { NeededByDate = "2026-12-01" });
        var assignBeforeApproval = await admin.PostAsJsonAsync($"/api/v1/tickets/{request.Id}/assign", new { assignedToId = agent.Id });
        var agentApprove = await agent.Client.PostAsJsonAsync($"/api/v1/tickets/{request.Id}/approve", new { comment = "ok" });
        var approved = await Post<TicketData>(manager.Client, $"/api/v1/tickets/{request.Id}/approve", new { comment = "Presupuesto disponible" });
        var assignedAfter = await Post<TicketData>(admin, $"/api/v1/tickets/{request.Id}/assign", new { assignedToId = agent.Id });

        var other = await Post<TicketData>(requester.Client, "/api/v1/tickets", NewTicket(equipment.Id));
        var rejectWithoutReason = await manager.Client.PostAsJsonAsync($"/api/v1/tickets/{other.Id}/reject", new { });
        var rejected = await Post<TicketData>(manager.Client, $"/api/v1/tickets/{other.Id}/reject", new { comment = "Fuera de presupuesto" });

        request.Type.Should().Be(TicketType.ServiceRequest);
        request.ApprovalStatus.Should().Be(ApprovalStatus.Pending);
        request.Sla.DueAtResolution.Should().BeNull("el SLA arranca al aprobar");
        (await ErrorCode(assignBeforeApproval, HttpStatusCode.Conflict)).Should().Be("TICKET_PENDING_APPROVAL");
        agentApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden, "TI_SUPPORT no tiene REQUESTS.APPROVE");
        approved.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        approved.Sla.DueAtResolution.Should().NotBeNull();
        assignedAfter.Status.Should().Be(TicketStatus.Open);
        (await ErrorCode(rejectWithoutReason, HttpStatusCode.BadRequest)).Should().Be("VALIDATION_ERROR");
        rejected.Status.Should().Be(TicketStatus.Cancelled);
        rejected.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
    }

    [Fact]
    public async Task OverdueFilter_ShouldListOpenTicketsPastTheirDeadline()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        var agent = await CreateUserAsync(SystemRoles.TiSupport);
        var category = await CategoryAsync("EMAIL");
        var late = await Post<TicketData>(requester.Client, "/api/v1/tickets", NewTicket(category.Id));
        var onTime = await Post<TicketData>(requester.Client, "/api/v1/tickets", NewTicket(category.Id));

        // Simula el paso del tiempo: el vencimiento de respuesta quedo en el pasado.
        await factory.WithDbContextAsync(async db =>
        {
            var ticket = await db.Tickets.SingleAsync(t => t.Id == late.Id);
            db.Entry(ticket).Property(t => t.DueAtResponse).CurrentValue = DateTime.UtcNow.AddHours(-2);
            return await db.SaveChangesAsync();
        });

        var overdue = await Get<PagedData<TicketData>>(agent.Client, "/api/v1/tickets?overdue=true&pageSize=200");
        var detail = await Get<TicketData>(agent.Client, $"/api/v1/tickets/{late.Id}");

        overdue.Items.Select(t => t.Id).Should().Contain(late.Id).And.NotContain(onTime.Id);
        detail.Sla.ResponseBreached.Should().BeTrue();
        detail.Sla.IsSlaBreached.Should().BeTrue();
    }

    [Fact]
    public async Task SlaPolicies_MostSpecificPolicyShouldApplyAndDefaultIsProtected()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var agent = await CreateUserAsync(SystemRoles.TiSupport);
        var category = await CategoryAsync("ACCESS");

        var seeded = await Get<List<SlaData>>(admin, "/api/v1/sla-policies");
        var specific = await Post<SlaData>(admin, "/api/v1/sla-policies", new
        {
            name = $"Accesos 24x7 {Guid.NewGuid():N}"[..30], categoryId = category.Id, responseTimeMinutes = 10,
            resolutionTimeMinutes = 60, businessHoursOnly = false, isDefault = false, isActive = true
        });
        var ticket = await Post<TicketData>(agent.Client, "/api/v1/tickets", NewTicket(category.Id));
        var defaultPolicy = seeded.Single(p => p.IsDefault);
        var deleteDefault = await admin.DeleteAsync($"/api/v1/sla-policies/{defaultPolicy.Id}");
        var defaultWithCriteria = await admin.PostAsJsonAsync("/api/v1/sla-policies", new
        {
            name = "Default invalido", priority = "High", responseTimeMinutes = 10, resolutionTimeMinutes = 20, isDefault = true, isActive = true
        });
        var agentCreates = await agent.Client.PostAsJsonAsync("/api/v1/sla-policies", new
        {
            name = "No autorizado", responseTimeMinutes = 10, resolutionTimeMinutes = 20, isActive = true
        });

        seeded.Should().Contain(p => p.Priority == TicketPriority.Critical && p.ResponseTimeMinutes == 15 && !p.BusinessHoursOnly);
        ticket.SlaPolicyId.Should().Be(specific.Id);
        (ticket.Sla.DueAtResponse!.Value - ticket.CreatedAt).TotalMinutes.Should().BeApproximately(10, 1);
        (await ErrorCode(deleteDefault, HttpStatusCode.Conflict)).Should().Be("SLA_DEFAULT_REQUIRED");
        (await ErrorCode(defaultWithCriteria, HttpStatusCode.BadRequest)).Should().Be("SLA_DEFAULT_WITH_CRITERIA");
        agentCreates.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Categories_ShouldBeReadableByAllAndManagedWithConfigurationPermission()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        using var admin = await factory.CreateAdminClientAsync();
        var code = $"C{Guid.NewGuid().ToString("N")[..8]}".ToUpperInvariant();

        var list = await Get<List<CategoryData>>(requester.Client, "/api/v1/ticket-categories?isActive=true");
        var userCreate = await requester.Client.PostAsJsonAsync("/api/v1/ticket-categories", NewCategory(code));
        var created = await Post<CategoryData>(admin, "/api/v1/ticket-categories", NewCategory(code));
        var duplicate = await admin.PostAsJsonAsync("/api/v1/ticket-categories", NewCategory(code));
        var changeType = await admin.PutAsJsonAsync($"/api/v1/ticket-categories/{created.Id}", NewCategory(code) with { Type = "ServiceRequest" });

        list.Select(c => c.Code).Should().Contain(["HARDWARE", "REQ_EQUIPMENT"]);
        userCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        created.Code.Should().Be(code);
        (await ErrorCode(duplicate, HttpStatusCode.Conflict)).Should().Be("CATEGORY_CODE_ALREADY_EXISTS");
        (await ErrorCode(changeType, HttpStatusCode.BadRequest)).Should().Be("CATEGORY_TYPE_IMMUTABLE");
    }

    private async Task<TestUser> CreateUserAsync(string role)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var userName = $"tk{Guid.NewGuid().ToString("N")[..8]}";
        var user = await Post<IdData>(admin, "/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Tk", lastName = role
        });

        if (role != SystemRoles.User)
        {
            (await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles", new { roles = new[] { role } }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return new TestUser(user.Id, await factory.LoginFreshAsync(userName, Password));
    }

    private async Task<CategoryData> CategoryAsync(string code)
    {
        using var admin = await factory.CreateAdminClientAsync();
        return (await Get<List<CategoryData>>(admin, "/api/v1/ticket-categories")).Single(c => c.Code == code);
    }

    private static TicketBody NewTicket(int categoryId, string? priority = null) =>
        new("No funciona", "Descripcion detallada del problema", categoryId, priority, null);

    private static CategoryBody NewCategory(string code) => new(code, $"Categoria {code}", "Incident", "Medium", false, true);

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

    private sealed record TicketBody(string Title, string Description, int CategoryId, string? Priority, string? NeededByDate);

    private sealed record CategoryBody(string Code, string Name, string Type, string DefaultPriority, bool RequiresApproval, bool IsActive);

    private sealed record SlaInfo(DateTime? DueAtResponse, DateTime? DueAtResolution, DateTime? FirstResponseAt,
        bool ResponseBreached, bool ResolutionBreached, bool IsSlaBreached);

    private sealed record TicketData(
        int Id, string TicketNumber, TicketType Type, TicketPriority Priority, TicketStatus Status, int RequesterId,
        int? AssignedToId, ApprovalStatus? ApprovalStatus, string? ResolutionNotes, int? SlaPolicyId, DateTime CreatedAt, SlaInfo Sla);

    private sealed record HistoryData(TicketStatus? FromStatus, TicketStatus ToStatus, string? Comment);

    private sealed record CommentData(int Id, string Content, bool IsInternal);

    private sealed record CategoryData(int Id, string Code, TicketType Type, TicketPriority DefaultPriority);

    private sealed record SlaData(int Id, TicketPriority? Priority, int ResponseTimeMinutes, bool BusinessHoursOnly, bool IsDefault);

    private sealed record IdData(int Id);
}
