namespace TIAdmin.Tests.Functional;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TIAdmin.Application.Assets;
using Xunit;

/// <summary>Actas de entrega y devolucion de equipos (ADR-040).</summary>
public sealed class AssetHandoverEndpointsTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    [Fact]
    public async Task AssignAndReturn_ShouldArchiveDeliveryAndReturnActas()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await HandoverTestData.CreateUserAsync(admin);
        var asset = await HandoverTestData.CreateAssetAsync(manager);

        var assigned = await HandoverTestData.PostAsync<HandoverAsset>(manager, $"/api/v1/assets/{asset.Id}/assign",
            new { userId = user.Id, condition = "Nuevo, con cargador" });
        var assignmentId = assigned.CurrentAssignment!.Id;

        // Antes de devolver no hay acta de devolucion.
        var early = await manager.GetAsync($"/api/v1/assets/{asset.Id}/assignments/{assignmentId}/handover/return");
        early.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await HandoverTestData.PostAsync<HandoverAsset>(manager, $"/api/v1/assets/{asset.Id}/return", new { condition = "Uso normal" });

        var documents = await HandoverTestData.GetAsync<List<HandoverDocument>>(manager, $"/api/v1/documents?entityName=Asset&entityId={asset.Id}");
        var year = DateTime.UtcNow.AddHours(-6).Year;
        documents.Should().HaveCount(2);
        documents.Should().ContainSingle(d => d.Category == AssetHandoverService.DeliveryCategory
            && d.FileName == $"Acta-entrega-ENT-{year}-{assignmentId:000000}.pdf" && d.MimeType == "application/pdf");
        documents.Should().ContainSingle(d => d.Category == AssetHandoverService.ReturnCategory
            && d.FileName == $"Acta-devolucion-DEV-{year}-{assignmentId:000000}.pdf");

        foreach (var document in documents)
        {
            var download = await manager.GetAsync($"/api/v1/documents/{document.Id}/download");
            (await download.Content.ReadAsByteArrayAsync()).Should().StartWith("%PDF"u8.ToArray());
        }

        var delivery = await manager.GetAsync($"/api/v1/assets/{asset.Id}/assignments/{assignmentId}/handover/delivery");
        delivery.StatusCode.Should().Be(HttpStatusCode.OK, await delivery.Content.ReadAsStringAsync());
        delivery.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        delivery.Content.Headers.ContentDisposition!.FileNameStar.Should().Be($"Acta-entrega-ENT-{year}-{assignmentId:000000}.pdf");
        (await delivery.Content.ReadAsByteArrayAsync()).Should().StartWith("%PDF"u8.ToArray());

        var returned = await manager.GetAsync($"/api/v1/assets/{asset.Id}/assignments/{assignmentId}/handover/return");
        returned.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Handover_OfAnotherAssetsAssignment_ShouldReturn404()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await HandoverTestData.CreateUserAsync(admin);
        var asset = await HandoverTestData.CreateAssetAsync(manager);
        var other = await HandoverTestData.CreateAssetAsync(manager);
        var assigned = await HandoverTestData.PostAsync<HandoverAsset>(manager, $"/api/v1/assets/{asset.Id}/assign", new { userId = user.Id });

        var response = await manager.GetAsync($"/api/v1/assets/{other.Id}/assignments/{assigned.CurrentAssignment!.Id}/handover/delivery");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

/// <summary>Si el PDF falla, la asignacion ya guardada no se revierte: el acta se genera despues bajo demanda.</summary>
public sealed class AssetHandoverFailureTests(FailingHandoverApiFactory factory) : IClassFixture<FailingHandoverApiFactory>
{
    [Fact]
    public async Task Assign_WhenActaFails_ShouldStillAssignWithoutDocument()
    {
        using var manager = await factory.CreateAssetManagerClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        var user = await HandoverTestData.CreateUserAsync(admin);
        var asset = await HandoverTestData.CreateAssetAsync(manager);

        var assigned = await HandoverTestData.PostAsync<HandoverAsset>(manager, $"/api/v1/assets/{asset.Id}/assign", new { userId = user.Id });

        assigned.CurrentAssignment.Should().NotBeNull();
        (await HandoverTestData.GetAsync<List<HandoverDocument>>(manager, $"/api/v1/documents?entityName=Asset&entityId={asset.Id}"))
            .Should().BeEmpty();
        var orphans = Directory.Exists(factory.StorageRoot)
            ? Directory.EnumerateFiles(factory.StorageRoot, "*.pdf", SearchOption.AllDirectories)
            : [];
        orphans.Should().BeEmpty();
    }
}

public sealed class FailingHandoverApiFactory : TIAdminApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IHandoverDocumentRenderer, FailingRenderer>());
    }

    private sealed class FailingRenderer : IHandoverDocumentRenderer
    {
        public byte[] Render(HandoverActa acta) => throw new InvalidOperationException("Fallo simulado del PDF.");
    }
}

internal sealed record HandoverAsset(int Id, HandoverAssignment? CurrentAssignment);

internal sealed record HandoverAssignment(int Id);

internal sealed record HandoverDocument(int Id, string FileName, string MimeType, string? Category);

internal static class HandoverTestData
{
    public static async Task<HandoverAsset> CreateAssetAsync(HttpClient client) =>
        await PostAsync<HandoverAsset>(client, "/api/v1/assets", new
        {
            assetCode = $"H{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
            serialNumber = $"SN-{Guid.NewGuid().ToString("N")[..8]}",
            name = "Laptop de prueba",
            assetTypeId = 1,
            brand = "Dell",
            model = "Latitude 5440"
        });

    public static async Task<HandoverAsset> CreateUserAsync(HttpClient admin)
    {
        var userName = $"acta{Guid.NewGuid().ToString("N")[..8]}";
        return await PostAsync<HandoverAsset>(admin, "/api/v1/users", new
        {
            userName, email = $"{userName}@tiadmin.tests", password = "Usuario123!Test", firstName = "Ana", lastName = "Pérez"
        });
    }

    public static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options))!.Data!;
    }

    public static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(TestJson.Options))!.Data!;
    }
}
