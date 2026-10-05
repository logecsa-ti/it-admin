namespace TIAdmin.Application.Vendors;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>Contratos con proveedores y sus alertas de vencimiento (SPECS.md seccion 25).</summary>
public interface IContractService
{
    Task<PagedResult<ContractDto>> SearchAsync(PagedQuery query, ContractFilter filter, CancellationToken cancellationToken = default);

    Task<ContractDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ContractDto> CreateAsync(CreateContractRequest request, CancellationToken cancellationToken = default);

    /// <summary>Solo contratos en Draft o Active: un contrato terminado o renovado es historico.</summary>
    Task<ContractDto> UpdateAsync(int id, UpdateContractRequest request, CancellationToken cancellationToken = default);

    Task<ContractDto> ActivateAsync(int id, CancellationToken cancellationToken = default);

    Task<ContractDto> TerminateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Crea el contrato sucesor (activo) y marca el actual como Renewed. Devuelve el sucesor.</summary>
    Task<ContractDto> RenewAsync(int id, RenewContractRequest request, CancellationToken cancellationToken = default);

    /// <summary>Baja logica. Un contrato activo debe terminarse primero.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Contratos activos que vencen dentro de las ventanas de Alerts.Contract.Days, o ya vencidos.</summary>
    Task<IReadOnlyList<ContractAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default);
}

public sealed class ContractService(
    IUnitOfWork unitOfWork,
    IUserDirectory userDirectory,
    ISystemSettings settings,
    IClock clock)
    : IContractService
{
    public const string AlertDaysKey = "Alerts.Contract.Days";
    private static readonly int[] DefaultAlertDays = [90, 60, 30, 15, 7];

    private DateOnly Today => DateOnly.FromDateTime(clock.Today);

    public Task<PagedResult<ContractDto>> SearchAsync(PagedQuery query, ContractFilter filter, CancellationToken cancellationToken = default) =>
        unitOfWork.Contracts.SearchAsync(query, filter, Today, cancellationToken);

    public async Task<ContractDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.Contracts.GetDtoAsync(id, Today, cancellationToken) ?? throw new EntityNotFoundException("Contract", id);

    public async Task<ContractDto> CreateAsync(CreateContractRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var number = request.Number.Trim().ToUpperInvariant();
        if (await unitOfWork.Contracts.ExistsNumberAsync(number, cancellationToken))
        {
            throw new ConflictException("CONTRACT_NUMBER_ALREADY_EXISTS", $"El contrato {number} ya existe.");
        }

        await ValidateReferencesAsync(request, currentVendorId: null, currentResponsibleId: null, cancellationToken);

        var contract = new Contract { Number = number };
        Apply(contract, request);
        if (request.Activate)
        {
            contract.Activate();
        }

        await unitOfWork.Contracts.AddAsync(contract, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(contract.Id, cancellationToken);
    }

    public async Task<ContractDto> UpdateAsync(int id, UpdateContractRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contract = await FindAsync(id, cancellationToken);
        if (contract.Status is ContractStatus.Terminated or ContractStatus.Renewed)
        {
            throw new ConflictException("CONTRACT_CLOSED", $"El contrato esta en estado {contract.Status} y ya no puede editarse.");
        }

        await ValidateReferencesAsync(request, contract.VendorId, contract.ResponsibleUserId, cancellationToken);

        Apply(contract, request);
        unitOfWork.Contracts.Update(contract);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ContractDto> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var contract = await FindAsync(id, cancellationToken);
        contract.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ContractDto> TerminateAsync(int id, CancellationToken cancellationToken = default)
    {
        var contract = await FindAsync(id, cancellationToken);
        contract.Terminate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ContractDto> RenewAsync(int id, RenewContractRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contract = await FindAsync(id, cancellationToken);
        var number = request.Number.Trim().ToUpperInvariant();
        if (await unitOfWork.Contracts.ExistsNumberAsync(number, cancellationToken))
        {
            throw new ConflictException("CONTRACT_NUMBER_ALREADY_EXISTS", $"El contrato {number} ya existe.");
        }

        var successor = contract.Renew(number, request.StartDate, request.EndDate, request.Value);

        await unitOfWork.Contracts.AddAsync(successor, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(successor.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var contract = await FindAsync(id, cancellationToken);
        if (contract.Status == ContractStatus.Active)
        {
            throw new ConflictException("CONTRACT_ACTIVE", "No se puede eliminar un contrato activo; terminelo primero.");
        }

        unitOfWork.Contracts.Delete(contract);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContractAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var windows = await settings.GetIntListAsync(AlertDaysKey, DefaultAlertDays, cancellationToken);
        var today = Today;
        var rows = await unitOfWork.Contracts.GetActiveEndingByAsync(today.AddDays(AlertWindows.Max(windows)), cancellationToken);

        return rows
            .Select(row =>
            {
                var daysRemaining = row.EndDate.DayNumber - today.DayNumber;
                return new ContractAlertDto(
                    row.Id, row.Number, row.Name, row.VendorName, row.EndDate, daysRemaining,
                    AlertWindows.Resolve(daysRemaining, windows), daysRemaining < 0, row.AutoRenew, row.ResponsibleUserId);
            })
            .Where(alert => alert.IsExpired || alert.AlertWindowDays is not null)
            .OrderBy(alert => alert.EndDate)
            .ToList();
    }

    private async Task<Contract> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Contracts.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Contract", id);

    private async Task ValidateReferencesAsync(
        IContractData data,
        int? currentVendorId,
        int? currentResponsibleId,
        CancellationToken cancellationToken)
    {
        // Las referencias existentes se conservan aunque el proveedor/usuario haya cambiado de estado.
        if (data.VendorId != currentVendorId)
        {
            var vendor = await unitOfWork.Vendors.GetByIdAsync(data.VendorId, cancellationToken);
            if (vendor is not { Status: VendorStatus.Active })
            {
                throw new DomainValidationException("VENDOR_NOT_AVAILABLE", "El proveedor no existe o no esta activo.");
            }
        }

        if (data.ResponsibleUserId is { } userId && userId != currentResponsibleId)
        {
            var user = await userDirectory.FindAsync(userId, cancellationToken);
            if (user is not { IsActive: true })
            {
                throw new DomainValidationException("USER_NOT_AVAILABLE", "El responsable indicado no existe o esta inactivo.");
            }
        }
    }

    private static void Apply(Contract contract, IContractData data)
    {
        contract.Name = data.Name.Trim();
        contract.VendorId = data.VendorId;
        contract.Type = data.Type;
        contract.SetPeriod(data.StartDate, data.EndDate);
        contract.Value = data.Value;
        contract.Currency = string.IsNullOrWhiteSpace(data.Currency) ? "USD" : data.Currency.Trim().ToUpperInvariant();
        contract.AutoRenew = data.AutoRenew;
        contract.RenewalNoticeDays = data.RenewalNoticeDays ?? 30;
        contract.ResponsibleUserId = data.ResponsibleUserId;
        contract.Notes = string.IsNullOrWhiteSpace(data.Notes) ? null : data.Notes.Trim();
    }
}
