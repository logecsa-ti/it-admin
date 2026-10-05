namespace TIAdmin.Tests.Unit;

using System.Text;
using FluentAssertions;
using TIAdmin.Application.Assets;
using TIAdmin.Infrastructure.Services;
using Xunit;

public sealed class HandoverDocumentRendererTests
{
    private static HandoverActa Acta(HandoverKind kind, string? clause = "El usuario se compromete a cuidar el equipo.") => new(
        kind,
        kind == HandoverKind.Delivery ? "ENT-2026-000012" : "DEV-2026-000012",
        "TI Admin",
        new DateTime(2026, 10, 5, 15, 30, 0),
        "LAP-001",
        "Laptop Dell Latitude",
        "Laptop",
        "Dell",
        "Latitude 5440",
        "SN-123",
        "Oficina central",
        "Finanzas",
        "Ana Pérez",
        "ana@tiadmin.local",
        kind == HandoverKind.Delivery ? "Admin TI" : "Ana Pérez",
        kind == HandoverKind.Delivery ? "Ana Pérez" : "Admin TI",
        "Nuevo, con cargador",
        "Entrega inicial",
        clause,
        new DateTime(2026, 10, 5, 15, 31, 0));

    [Theory]
    [InlineData(HandoverKind.Delivery)]
    [InlineData(HandoverKind.Return)]
    public void Render_ShouldProducePdf(HandoverKind kind)
    {
        var pdf = new HandoverDocumentRenderer().Render(Acta(kind));

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
        pdf.Length.Should().BeGreaterThan(5_000);
    }

    [Fact]
    public void Render_WithoutOptionalData_ShouldNotFail()
    {
        var minimal = Acta(HandoverKind.Delivery, clause: null) with
        {
            Brand = null, Model = null, SerialNumber = null, Location = null, Department = null,
            HolderEmail = null, Condition = null, Notes = null
        };

        var pdf = new HandoverDocumentRenderer().Render(minimal);

        Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }
}
