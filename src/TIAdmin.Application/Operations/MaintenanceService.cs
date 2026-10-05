namespace TIAdmin.Application.Operations;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Mantenimientos de activos (SPECS.md seccion 26). Al iniciar uno sobre un activo disponible, el
/// activo pasa a Maintenance; al completarlo o cancelarlo vuelve a Available si no hay otro en curso.
/// </summary>
public interface IMaintenanceService
{
    Task<PagedResult<MaintenanceDto>> SearchAsync(PagedQuery query, MaintenanceFilter filter, CancellationToken cancellationToken = default);

    Task<MaintenanceDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<MaintenanceDto> CreateAsync(MaintenanceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Solo antes de iniciar (Planned/Scheduled).</summary>
    Task<MaintenanceDto> UpdateAsync(int id, MaintenanceRequest request, CancellationToken cancellationToken = default);

    Task<MaintenanceDto> ScheduleAsync(int id, DateTime scheduledDate, CancellationToken cancellationToken = default);

    Task<MaintenanceDto> StartAsync(int id, CancellationToken cancellationToken = default);

    Task<MaintenanceDto> CompleteAsync(int id, CompleteMaintenanceRequest request, CancellationToken cancellationToken = default);

    Task<MaintenanceDto> CancelAsync(int id, string reason, CancellationToken cancellationToken = default);

    /// <summary>Baja logica; no se permite en curso ni completado (es historial del activo).</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default);
}

public sealed class MaintenanceService(
    IUnitOfWork unitOfWork,
    IUserDirectory userDirectory,
    IPermissionService permissionService,
    AssetMovementLog movementLog,
    ISystemSettings settings,
    IClock clock)
    : IMaintenanceService
{
    public const string NumberPrefixKey = "Maintenance.NumberPrefix";
    public const string AlertDaysKey = "Alerts.Maintenance.Days";

    public Task<PagedResult<MaintenanceDto>> SearchAsync(PagedQuery query, MaintenanceFilter filter, CancellationToken cancellationToken = default) =>
        unitOfWork.Maintenances.SearchAsync(query, filter, clock.UtcNow, cancellationToken);

    public async Task<MaintenanceDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.Maintenances.GetDtoAsync(id, clock.UtcNow, cancellationToken) ?? throw new EntityNotFoundException("Maintenance", id);

    public async Task<MaintenanceDto> CreateAsync(MaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ValidateReferencesAsync(request, current: null, cancellationToken);

        var now = clock.UtcNow;
        var maintenance = new Maintenance { Number = DocumentNumbers.Provisional() };
        Apply(maintenance, request);

        await unitOfWork.Maintenances.AddAsync(maintenance, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        maintenance.Number = await DocumentNumbers.FormatAsync(settings, clock, NumberPrefixKey, "MNT", maintenance.Id, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(maintenance.Id, cancellationToken);
    }

    public async Task<MaintenanceDto> UpdateAsync(int id, MaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maintenance = await FindAsync(id, cancellationToken);
        if (maintenance.Status is not (MaintenanceStatus.Planned or MaintenanceStatus.Scheduled))
        {
            throw new ConflictException("MAINTENANCE_NOT_EDITABLE", $"No se puede editar un mantenimiento en estado {maintenance.Status}.");
        }

        await ValidateReferencesAsync(request, maintenance, cancellationToken);
        Apply(maintenance, request);

        unitOfWork.Maintenances.Update(maintenance);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<MaintenanceDto> ScheduleAsync(int id, DateTime scheduledDate, CancellationToken cancellationToken = default)
    {
        var maintenance = await FindAsync(id, cancellationToken);
        maintenance.Schedule(DateTime.SpecifyKind(scheduledDate, DateTimeKind.Utc));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<MaintenanceDto> StartAsync(int id, CancellationToken cancellationToken = default)
    {
        var maintenance = await FindAsync(id, cancellationToken);
        maintenance.Start(clock.UtcNow);

        // Un activo asignado puede recibir mantenimiento sin cambiar de estado (p. ej. preventivo en sitio).
        var asset = await unitOfWork.Assets.GetByIdAsync(maintenance.AssetId, cancellationToken);
        if (asset is { Status: AssetStatus.Available })
        {
            asset.ChangeStatus(AssetStatus.Maintenance);
            await movementLog.AddAsync(asset, AssetMovementType.StatusChange, nameof(AssetStatus.Available),
                nameof(AssetStatus.Maintenance), $"Mantenimiento {maintenance.Number}", cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<MaintenanceDto> CompleteAsync(int id, CompleteMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maintenance = await FindAsync(id, cancellationToken);
        maintenance.Complete(clock.UtcNow, request.Actions, Normalize(request.Findings), Normalize(request.Recommendations),
            request.ActualCost, request.NextDueDate);

        await ReleaseAssetAsync(maintenance, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<MaintenanceDto> CancelAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        var maintenance = await FindAsync(id, cancellationToken);
        var wasInProgress = maintenance.Status == MaintenanceStatus.InProgress;
        maintenance.Cancel(reason);

        if (wasInProgress)
        {
            await ReleaseAssetAsync(maintenance, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var maintenance = await FindAsync(id, cancellationToken);
        if (maintenance.Status is MaintenanceStatus.InProgress or MaintenanceStatus.Completed)
        {
            throw new ConflictException("MAINTENANCE_NOT_DELETABLE",
                "Un mantenimiento en curso o completado no se elimina: es parte del historial del activo.");
        }

        unitOfWork.Maintenances.Delete(maintenance);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MaintenanceAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var days = await settings.GetIntAsync(AlertDaysKey, 7, cancellationToken);
        var now = clock.UtcNow;

        var pending = await unitOfWork.Maintenances.GetPendingUntilAsync(now.AddDays(days), now, cancellationToken);
        var preventive = await unitOfWork.Maintenances.GetPreventiveDueAsync(
            DateOnly.FromDateTime(clock.Today).AddDays(days), cancellationToken);

        return pending.Concat(preventive).OrderBy(a => a.DueDate).ToList();
    }

    private async Task<Maintenance> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Maintenances.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Maintenance", id);

    /// <summary>Devuelve el activo a Available si esta en Maintenance y no queda otro mantenimiento en curso.</summary>
    private async Task ReleaseAssetAsync(Maintenance maintenance, CancellationToken cancellationToken)
    {
        var asset = await unitOfWork.Assets.GetByIdAsync(maintenance.AssetId, cancellationToken);
        if (asset is not { Status: AssetStatus.Maintenance }
            || await unitOfWork.Maintenances.HasOtherInProgressAsync(asset.Id, maintenance.Id, cancellationToken))
        {
            return;
        }

        asset.ChangeStatus(AssetStatus.Available);
        await movementLog.AddAsync(asset, AssetMovementType.StatusChange, nameof(AssetStatus.Maintenance),
            nameof(AssetStatus.Available), $"Fin de mantenimiento {maintenance.Number}", cancellationToken);
    }

    private async Task ValidateReferencesAsync(IMaintenanceData data, Maintenance? current, CancellationToken cancellationToken)
    {
        if (data.AssetId != current?.AssetId && !await unitOfWork.Assets.ExistsAsync(data.AssetId, cancellationToken))
        {
            throw new DomainValidationException("ASSET_NOT_FOUND", "El activo indicado no existe.");
        }

        if (data.TechnicianId is { } technicianId && technicianId != current?.TechnicianId)
        {
            if (await userDirectory.FindAsync(technicianId, cancellationToken) is not { IsActive: true })
            {
                throw new DomainValidationException("USER_NOT_AVAILABLE", "El tecnico indicado no existe o esta inactivo.");
            }

            var permissions = await permissionService.GetPermissionsAsync(technicianId, cancellationToken);
            if (!permissions.Contains(Permissions.MaintenanceManage, StringComparer.OrdinalIgnoreCase))
            {
                throw new DomainValidationException("TECHNICIAN_NOT_QUALIFIED", "El tecnico debe tener el permiso MAINTENANCE.MANAGE.");
            }
        }

        if (data.VendorId is { } vendorId && vendorId != current?.VendorId)
        {
            var vendor = await unitOfWork.Vendors.GetByIdAsync(vendorId, cancellationToken);
            if (vendor is not { Status: VendorStatus.Active })
            {
                throw new DomainValidationException("VENDOR_NOT_AVAILABLE", "El proveedor no existe o no esta activo.");
            }
        }

        if (data.TicketId is { } ticketId && !await unitOfWork.Tickets.ExistsAsync(ticketId, cancellationToken))
        {
            throw new DomainValidationException("TICKET_NOT_FOUND", "El ticket indicado no existe.");
        }

        if (data.ContractId is { } contractId)
        {
            var contract = await unitOfWork.Contracts.GetByIdAsync(contractId, cancellationToken)
                ?? throw new DomainValidationException("CONTRACT_NOT_FOUND", "El contrato indicado no existe.");

            if (data.VendorId is { } vendor && contract.VendorId != vendor)
            {
                throw new DomainValidationException("CONTRACT_VENDOR_MISMATCH", "El contrato pertenece a otro proveedor.");
            }
        }
    }

    private static void Apply(Maintenance maintenance, IMaintenanceData data)
    {
        maintenance.Title = data.Title.Trim();
        maintenance.Description = Normalize(data.Description);
        maintenance.Type = data.Type;
        maintenance.AssetId = data.AssetId;
        maintenance.TechnicianId = data.TechnicianId;
        maintenance.VendorId = data.VendorId;
        maintenance.TicketId = data.TicketId;
        maintenance.ContractId = data.ContractId;
        maintenance.ScheduledDate = DateTime.SpecifyKind(data.ScheduledDate, DateTimeKind.Utc);
        maintenance.EstimatedCost = data.EstimatedCost;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
