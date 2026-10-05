namespace TIAdmin.Infrastructure.Services;

using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TIAdmin.Application.Assets;

/// <summary>
/// Actas de entrega y devolucion en PDF con QuestPDF (Q-03, ADR-040). Licencia Community: gratuita para
/// organizaciones con ingresos anuales menores a USD 1 millon; por encima se requiere licencia comercial.
/// </summary>
public sealed class HandoverDocumentRenderer : IHandoverDocumentRenderer
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-NI");
    private const string Accent = "#3F4F1A";
    private const string Muted = "#6B6E62";
    private const string Border = "#D5D7CC";

    static HandoverDocumentRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(HandoverActa acta)
    {
        ArgumentNullException.ThrowIfNull(acta);
        var delivery = acta.Kind == HandoverKind.Delivery;

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(40);
            page.DefaultTextStyle(text => text.FontSize(10).FontColor(Colors.Black));

            page.Header().Element(header => Header(header, acta, delivery));
            page.Content().PaddingVertical(16).Column(column =>
            {
                column.Spacing(14);
                column.Item().Text(Introduction(acta, delivery)).LineHeight(1.4f);

                column.Item().Element(section => Section(section, "Datos del equipo", table =>
                {
                    Row(table, "Código", acta.AssetCode);
                    Row(table, "Descripción", acta.AssetName);
                    Row(table, "Tipo", acta.AssetType);
                    Row(table, "Marca / modelo", JoinOrDash(acta.Brand, acta.Model));
                    Row(table, "Número de serie", acta.SerialNumber);
                    Row(table, "Ubicación", acta.Location);
                    Row(table, "Departamento", acta.Department);
                }));

                column.Item().Element(section => Section(section, delivery ? "Datos de la entrega" : "Datos de la devolución", table =>
                {
                    Row(table, delivery ? "Entregado a" : "Devuelto por", acta.HolderName);
                    Row(table, "Correo", acta.HolderEmail);
                    Row(table, delivery ? "Entregado por" : "Recibido por", delivery ? acta.DeliveredBy : acta.ReceivedBy);
                    Row(table, "Fecha y hora", acta.Date.ToString("dd 'de' MMMM 'de' yyyy, HH:mm", Spanish));
                    Row(table, delivery ? "Condición de entrega" : "Condición al devolver", acta.Condition);
                    if (delivery)
                    {
                        Row(table, "Observaciones", acta.Notes);
                    }
                }));

                if (acta.Clause is not null)
                {
                    column.Item().Element(section => section
                        .Background("#F4F5EF").Border(1).BorderColor(Border).Padding(12)
                        .Column(clause =>
                        {
                            clause.Spacing(6);
                            clause.Item().Text(delivery ? "Compromiso del usuario" : "Constancia de devolución").Bold().FontColor(Accent);
                            clause.Item().Text(acta.Clause).LineHeight(1.4f).Justify();
                        }));
                }

                column.Item().PaddingTop(48).Row(row =>
                {
                    row.Spacing(48);
                    row.RelativeItem().Element(cell => Signature(cell, delivery ? "Entrega (TI)" : "Entrega (usuario)", acta.DeliveredBy));
                    row.RelativeItem().Element(cell => Signature(cell, delivery ? "Recibe (usuario)" : "Recibe (TI)", acta.ReceivedBy));
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor(Muted));
                text.Span($"{acta.Number} · Generado por {acta.OrganizationName} el {acta.GeneratedAt.ToString("dd/MM/yyyy HH:mm", Spanish)} · Página ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        })).GeneratePdf();
    }

    private static void Header(IContainer container, HandoverActa acta, bool delivery) =>
        container.BorderBottom(2).BorderColor(Accent).PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(acta.OrganizationName).FontSize(16).Bold().FontColor(Accent);
                column.Item().Text("Departamento de Tecnologías de Información").FontColor(Muted);
            });
            row.ConstantItem(220).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text(delivery ? "ACTA DE ENTREGA DE EQUIPO" : "ACTA DE DEVOLUCIÓN DE EQUIPO").FontSize(12).Bold();
                column.Item().AlignRight().Text($"N.º {acta.Number}").FontColor(Muted);
            });
        });

    private static string Introduction(HandoverActa acta, bool delivery)
    {
        var date = acta.Date.ToString("d 'de' MMMM 'de' yyyy", Spanish);
        return delivery
            ? $"En fecha {date}, el área de Tecnologías de Información hace entrega a {acta.HolderName} del equipo descrito a continuación, quien lo recibe en las condiciones indicadas."
            : $"En fecha {date}, {acta.HolderName} devuelve al área de Tecnologías de Información el equipo descrito a continuación, en las condiciones indicadas.";
    }

    private static void Section(IContainer container, string title, Action<TableDescriptor> rows) =>
        container.Column(column =>
        {
            column.Item().PaddingBottom(4).Text(title).FontSize(11).Bold().FontColor(Accent);
            column.Item().Border(1).BorderColor(Border).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });
                rows(table);
            });
        });

    private static void Row(TableDescriptor table, string label, string? value)
    {
        table.Cell().BorderBottom(1).BorderColor(Border).Background("#F4F5EF").PaddingVertical(5).PaddingHorizontal(8)
            .Text(label).FontColor(Muted);
        table.Cell().BorderBottom(1).BorderColor(Border).PaddingVertical(5).PaddingHorizontal(8)
            .Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
    }

    private static void Signature(IContainer container, string role, string name) =>
        container.Column(column =>
        {
            column.Item().BorderTop(1).BorderColor(Colors.Black).PaddingTop(4).AlignCenter().Text(name).Bold();
            column.Item().AlignCenter().Text(role).FontColor(Muted);
            column.Item().PaddingTop(4).AlignCenter().Text("Firma y fecha").FontSize(8).FontColor(Muted);
        });

    private static string? JoinOrDash(string? first, string? second)
    {
        var parts = new[] { first, second }.Where(p => !string.IsNullOrWhiteSpace(p));
        var joined = string.Join(" ", parts);
        return joined.Length == 0 ? null : joined;
    }
}
