namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Platform;
using Xunit;

public sealed class PlatformEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    private const string Password = "Usuario123!Test";

    [Fact]
    public async Task Document_UploadListDownloadDelete_ShouldStoreFileOutsideDatabase()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var asset = await CreateAssetAsync(manager);
        var bytes = "%PDF-1.4 factura de compra"u8.ToArray();

        var uploaded = await UploadAsync(manager, "Asset", asset, "../../factura compra.pdf", bytes);
        var list = await Get<List<DocumentData>>(manager, $"/api/v1/documents?entityName=asset&entityId={asset}");
        var download = await manager.GetAsync($"/api/v1/documents/{uploaded.Id}/download");
        var downloaded = await download.Content.ReadAsByteArrayAsync();
        var storagePath = await factory.WithDbContextAsync(db => db.Documents.Where(d => d.Id == uploaded.Id).Select(d => d.StoragePath).SingleAsync());

        uploaded.FileName.Should().Be("factura compra.pdf", "el nombre se sanea: sin rutas");
        uploaded.MimeType.Should().Be("application/pdf");
        uploaded.Size.Should().Be(bytes.Length);
        uploaded.Checksum.Should().Be(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        list.Should().ContainSingle(d => d.Id == uploaded.Id);
        downloaded.Should().Equal(bytes);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        storagePath.Should().StartWith("asset/").And.NotContain("factura");
        File.Exists(Path.Combine(factory.StorageRoot, storagePath)).Should().BeTrue();

        (await manager.DeleteAsync($"/api/v1/documents/{uploaded.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Get<List<DocumentData>>(manager, $"/api/v1/documents?entityName=Asset&entityId={asset}")).Should().BeEmpty();
        File.Exists(Path.Combine(factory.StorageRoot, storagePath)).Should().BeTrue("la baja es logica: el archivo se conserva");
    }

    [Fact]
    public async Task Document_ShouldRejectBadFilesUnknownEntitiesAndUnauthorizedUsers()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        var endUser = await CreateUserAsync(SystemRoles.User);
        var asset = await CreateAssetAsync(manager);

        var exe = await UploadRawAsync(manager, "Asset", asset, "setup.exe", [1, 2, 3]);
        var empty = await UploadRawAsync(manager, "Asset", asset, "vacio.txt", []);
        var unknownEntity = await UploadRawAsync(manager, "Planeta", 1, "a.txt", [1]);
        var missingEntity = await UploadRawAsync(manager, "Asset", 999_999, "a.txt", [1]);
        var endUserUpload = await UploadRawAsync(endUser.Client, "Asset", asset, "a.txt", [1]);

        (await ErrorCode(exe, HttpStatusCode.BadRequest)).Should().Be("FILE_TYPE_NOT_ALLOWED");
        (await ErrorCode(empty, HttpStatusCode.BadRequest)).Should().Be("FILE_EMPTY");
        (await ErrorCode(unknownEntity, HttpStatusCode.BadRequest)).Should().Be("ENTITY_NOT_SUPPORTED");
        missingEntity.StatusCode.Should().Be(HttpStatusCode.NotFound);
        endUserUpload.StatusCode.Should().Be(HttpStatusCode.Forbidden, "el rol USER no ve activos");
    }

    [Fact]
    public async Task TicketAttachments_ShouldFollowTicketVisibility()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        var stranger = await CreateUserAsync(SystemRoles.User);
        var agent = await CreateUserAsync(SystemRoles.TiSupport);
        var ticket = await CreateTicketAsync(requester.Client);

        var screenshot = await UploadAsync(requester.Client, "Ticket", ticket, "captura.png", [137, 80, 78, 71]);
        var agentNote = await UploadAsync(agent.Client, "Ticket", ticket, "diagnostico.txt", "disco con sectores danados"u8.ToArray());
        var strangerList = await stranger.Client.GetAsync($"/api/v1/documents?entityName=Ticket&entityId={ticket}");
        var agentList = await Get<List<DocumentData>>(agent.Client, $"/api/v1/documents?entityName=Ticket&entityId={ticket}");
        var requesterDeletesAgentNote = await requester.Client.DeleteAsync($"/api/v1/documents/{agentNote.Id}");
        var requesterDeletesOwn = await requester.Client.DeleteAsync($"/api/v1/documents/{screenshot.Id}");

        strangerList.StatusCode.Should().Be(HttpStatusCode.NotFound, "un ticket ajeno no existe para el usuario");
        agentList.Select(d => d.Id).Should().Contain([screenshot.Id, agentNote.Id]);
        requesterDeletesAgentNote.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        requesterDeletesOwn.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Notifications_ShouldReachTheRightPeopleAndBeMarkedRead()
    {
        var requester = await CreateUserAsync(SystemRoles.User);
        var agent = await CreateUserAsync(SystemRoles.TiSupport);
        using var admin = await factory.CreateAdminClientAsync();
        var ticket = await CreateTicketAsync(requester.Client);

        await Post<object>(admin, $"/api/v1/tickets/{ticket}/assign", new { assignedToId = agent.Id });
        await Post<object>(agent.Client, $"/api/v1/tickets/{ticket}/status", new { status = "InProgress" });
        await Post<object>(agent.Client, $"/api/v1/tickets/{ticket}/status", new { status = "Resolved", resolutionNotes = "Listo" });

        var agentInbox = await Get<PagedData<NotificationData>>(agent.Client, "/api/v1/notifications");
        var requesterInbox = await Get<PagedData<NotificationData>>(requester.Client, "/api/v1/notifications?unreadOnly=true");
        var unreadBefore = await Get<int>(requester.Client, "/api/v1/notifications/unread-count");
        await Post<object>(requester.Client, $"/api/v1/notifications/{requesterInbox.Items[0].Id}/read", new { });
        var unreadAfter = await Get<int>(requester.Client, "/api/v1/notifications/unread-count");
        var othersNotification = await agent.Client.PostAsync($"/api/v1/notifications/{requesterInbox.Items[0].Id}/read", null);

        agentInbox.Items.Should().ContainSingle(n => n.Type == "TicketAssigned" && n.EntityId == ticket);
        agentInbox.Items.Should().NotContain(n => n.Type == "TicketStatusChanged", "quien provoca el evento no se notifica a si mismo");
        requesterInbox.Items.Should().ContainSingle(n => n.Type == "TicketStatusChanged" && n.Link == $"/tickets/{ticket}");
        unreadBefore.Should().Be(1);
        unreadAfter.Should().Be(0);
        othersNotification.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ApprovalRequests_ShouldNotifyApprovers()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var approver = await CreateUserAsync(SystemRoles.TiManager);

        var purchase = await Post<IdData>(admin, "/api/v1/purchases", new
        {
            title = "Notificar aprobador", items = new[] { new { description = "Toner", quantity = 2, unitPrice = 60m } }
        });
        await Post<object>(admin, $"/api/v1/purchases/{purchase.Id}/submit", new { });
        await Post<object>(approver.Client, $"/api/v1/purchases/{purchase.Id}/approve", new { });

        var approverInbox = await Get<PagedData<NotificationData>>(approver.Client, "/api/v1/notifications");
        var adminInbox = await Get<PagedData<NotificationData>>(admin, "/api/v1/notifications?pageSize=200");

        approverInbox.Items.Should().Contain(n => n.Type == "PurchaseApprovalRequired" && n.EntityId == purchase.Id);
        adminInbox.Items.Should().Contain(n => n.Type == "PurchaseApproved" && n.EntityId == purchase.Id);
        adminInbox.Items.Should().NotContain(n => n.Type == "PurchaseApprovalRequired" && n.EntityId == purchase.Id,
            "el solicitante no recibe su propia solicitud de aprobacion");
    }

    [Fact]
    public async Task AlertJob_ShouldNotifyOncePerAlertWindow()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var responsible = await CreateUserAsync(SystemRoles.TiManager);
        var vendor = await Post<IdData>(admin, "/api/v1/vendors", new { name = $"Alerta {Guid.NewGuid():N}"[..30], status = "Active" });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var contract = await Post<IdData>(admin, "/api/v1/contracts", new
        {
            number = $"AL-{Guid.NewGuid().ToString("N")[..8]}".ToUpperInvariant(), name = "Soporte", vendorId = vendor.Id, type = "Support",
            startDate = today.AddYears(-1), endDate = today.AddDays(20), responsibleUserId = responsible.Id, activate = true
        });

        var first = await RunAlertJobAsync();
        var second = await RunAlertJobAsync();
        var inbox = await Get<PagedData<NotificationData>>(responsible.Client, "/api/v1/notifications?pageSize=200");

        first.Contracts.Should().BeGreaterThanOrEqualTo(1);
        second.Contracts.Should().Be(0, "la misma ventana no se notifica dos veces");
        inbox.Items.Should().ContainSingle(n => n.Type == "ContractExpiring" && n.EntityId == contract.Id);
    }

    private async Task<AlertScanResult> RunAlertJobAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAlertNotificationJob>().RunAsync();
    }

    private async Task<TestUser> CreateUserAsync(string role)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var userName = $"pf{Guid.NewGuid().ToString("N")[..8]}";
        var user = await Post<IdData>(admin, "/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = Password, firstName = "Pf", lastName = role
        });

        if (role != SystemRoles.User)
        {
            await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/roles", new { roles = new[] { role } });
        }

        return new TestUser(user.Id, await factory.LoginFreshAsync(userName, Password));
    }

    private async Task<int> CreateTicketAsync(HttpClient requester)
    {
        var category = (await Get<List<CategoryData>>(requester, "/api/v1/ticket-categories")).First(c => c.Type == "Incident");
        return (await Post<IdData>(requester, "/api/v1/tickets", new { title = "Falla", description = "Detalle", categoryId = category.Id })).Id;
    }

    private static async Task<int> CreateAssetAsync(HttpClient client) =>
        (await Post<IdData>(client, "/api/v1/assets", new
        {
            assetCode = $"DOC{Guid.NewGuid().ToString("N")[..9].ToUpperInvariant()}", name = "Equipo", assetTypeId = 1
        })).Id;

    private static async Task<DocumentData> UploadAsync(HttpClient client, string entity, int entityId, string fileName, byte[] bytes)
    {
        var response = await UploadRawAsync(client, entity, entityId, fileName, bytes);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<DocumentData>>(TestJson.Options))!.Data!;
    }

    private static async Task<HttpResponseMessage> UploadRawAsync(HttpClient client, string entity, int entityId, string fileName, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        form.Add(new StringContent(entity), "entityName");
        form.Add(new StringContent(entityId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "entityId");
        return await client.PostAsync("/api/v1/documents", form);
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

    private sealed record CategoryData(int Id, string Type);

    private sealed record DocumentData(int Id, string FileName, string MimeType, long Size, string? Checksum, string EntityName, int EntityId);

    private sealed record NotificationData(int Id, string Type, string Title, string? Link, int? EntityId, bool IsRead);
}
