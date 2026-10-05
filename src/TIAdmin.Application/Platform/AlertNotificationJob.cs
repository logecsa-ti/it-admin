namespace TIAdmin.Application.Platform;

using System.Globalization;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Application.Licensing;
using TIAdmin.Application.Operations;
using TIAdmin.Application.Vendors;

public record AlertScanResult(int Contracts, int Licenses, int Maintenance, int Tickets)
{
    public int Total => Contracts + Licenses + Maintenance + Tickets;
}

/// <summary>
/// Barrido periodico de alertas (SPECS.md secciones 22-26): convierte las alertas de contratos, licencias,
/// mantenimientos y SLA vencidos en notificaciones. Cada alerta lleva una clave de deduplicacion, de modo
/// que se notifica una sola vez por ventana aunque el barrido se ejecute muchas veces.
/// </summary>
public interface IAlertNotificationJob
{
    Task<AlertScanResult> RunAsync(CancellationToken cancellationToken = default);
}

public sealed class AlertNotificationJob(
    IContractService contracts,
    ILicenseService licenses,
    IMaintenanceService maintenance,
    IUnitOfWork unitOfWork,
    INotificationService notifications,
    IClock clock)
    : IAlertNotificationJob
{
    public async Task<AlertScanResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var contractCount = 0;
        foreach (var alert in await contracts.GetAlertsAsync(cancellationToken))
        {
            var window = alert.IsExpired ? "expired" : alert.AlertWindowDays!.Value.ToString(CultureInfo.InvariantCulture);
            var request = new NotificationRequest([], alert.IsExpired ? "ContractExpired" : "ContractExpiring",
                alert.IsExpired ? $"Contrato {alert.Number} vencido" : $"Contrato {alert.Number} vence en {alert.DaysRemaining} dias",
                $"{alert.Name} - {alert.VendorName} (vence {alert.EndDate:yyyy-MM-dd})",
                "Contract", alert.ContractId, $"/contracts/{alert.ContractId}", DedupKey: $"contract:{alert.ContractId}:{window}");

            // El responsable del contrato; sin responsable, quienes administran contratos.
            contractCount += alert.ResponsibleUserId is { } responsible
                ? await notifications.NotifyAsync(request with { UserIds = [responsible] }, cancellationToken)
                : await notifications.NotifyPermissionAsync(Permissions.ContractsManage, request, cancellationToken);
        }

        var licenseCount = 0;
        foreach (var alert in await licenses.GetAlertsAsync(cancellationToken))
        {
            var (title, key) = alert.AlertType switch
            {
                LicenseAlertType.ExpiringSoon => ($"Licencia {alert.Name} vence en {alert.DaysRemaining} dias", $"license:{alert.LicenseId}:expiring:{alert.AlertWindowDays}"),
                LicenseAlertType.Expired => ($"Licencia {alert.Name} vencida", $"license:{alert.LicenseId}:expired"),
                LicenseAlertType.Exhausted => ($"Licencia {alert.Name} sin puestos disponibles", $"license:{alert.LicenseId}:exhausted:{alert.Quantity}"),
                _ => ($"Licencia {alert.Name} subutilizada", $"license:{alert.LicenseId}:low")
            };

            licenseCount += await notifications.NotifyPermissionAsync(Permissions.LicensesManage,
                new NotificationRequest([], $"License{alert.AlertType}", title,
                    $"{alert.SoftwareName}: {alert.UsedQuantity}/{alert.Quantity} puestos en uso",
                    "License", alert.LicenseId, $"/licenses/{alert.LicenseId}", DedupKey: key),
                cancellationToken);
        }

        var maintenanceCount = 0;
        foreach (var alert in await maintenance.GetAlertsAsync(cancellationToken))
        {
            var title = alert.AlertType switch
            {
                MaintenanceAlertType.Overdue => $"Mantenimiento {alert.MaintenanceNumber} vencido",
                MaintenanceAlertType.Upcoming => $"Mantenimiento {alert.MaintenanceNumber} programado",
                _ => $"Preventivo pendiente para {alert.AssetCode}"
            };

            maintenanceCount += await notifications.NotifyPermissionAsync(Permissions.MaintenanceManage,
                new NotificationRequest([], $"Maintenance{alert.AlertType}", title,
                    $"Activo {alert.AssetCode}, fecha {alert.DueDate:yyyy-MM-dd}",
                    alert.MaintenanceId is null ? "Asset" : "Maintenance", alert.MaintenanceId ?? alert.AssetId,
                    alert.MaintenanceId is { } id ? $"/maintenances/{id}" : $"/assets/{alert.AssetId}",
                    DedupKey: $"maintenance:{alert.AlertType}:{alert.MaintenanceId ?? alert.AssetId}:{alert.DueDate:yyyyMMdd}"),
                cancellationToken);
        }

        var ticketCount = 0;
        foreach (var breach in await unitOfWork.Tickets.GetOpenSlaBreachesAsync(clock.UtcNow, cancellationToken))
        {
            var kind = breach.ResolutionBreached ? "resolution" : "response";
            var request = new NotificationRequest([], "TicketSlaBreached",
                $"SLA vencido en {breach.TicketNumber}",
                $"{breach.Title} ({(breach.ResolutionBreached ? "resolucion" : "primera respuesta")} vencida)",
                "Ticket", breach.Id, $"/tickets/{breach.Id}", DedupKey: $"ticket:{breach.Id}:sla:{kind}");

            // El responsable; si nadie lo tiene asignado, quienes asignan tickets.
            ticketCount += breach.AssignedToId is { } assignee
                ? await notifications.NotifyAsync(request with { UserIds = [assignee] }, cancellationToken)
                : await notifications.NotifyPermissionAsync(Permissions.TicketsAssign, request, cancellationToken);
        }

        return new AlertScanResult(contractCount, licenseCount, maintenanceCount, ticketCount);
    }
}
