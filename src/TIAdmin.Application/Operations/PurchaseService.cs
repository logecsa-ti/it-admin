namespace TIAdmin.Application.Operations;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Compras de TI. Aprobar/rechazar: PURCHASES.APPROVE (nunca el solicitante). Ordenar/recibir:
/// PURCHASES.MANAGE. Editar, enviar o cancelar la propia solicitud: el solicitante o PURCHASES.MANAGE.
/// </summary>
public interface IPurchaseService
{
    Task<PagedResult<PurchaseRequestDto>> SearchAsync(PagedQuery query, PurchaseFilter filter, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> CreateAsync(PurchaseRequestBody request, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> UpdateAsync(int id, PurchaseRequestBody request, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> SubmitAsync(int id, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> ApproveAsync(int id, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> MarkOrderedAsync(int id, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> MarkReceivedAsync(int id, CancellationToken cancellationToken = default);

    Task<PurchaseRequestDto> CancelAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class PurchaseService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    ISystemSettings settings,
    IClock clock)
    : IPurchaseService
{
    public const string NumberPrefixKey = "Purchases.NumberPrefix";

    public Task<PagedResult<PurchaseRequestDto>> SearchAsync(PagedQuery query, PurchaseFilter filter, CancellationToken cancellationToken = default) =>
        unitOfWork.PurchaseRequests.SearchAsync(query, filter, cancellationToken);

    public async Task<PurchaseRequestDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.PurchaseRequests.GetDtoAsync(id, cancellationToken) ?? throw new EntityNotFoundException("PurchaseRequest", id);

    public async Task<PurchaseRequestDto> CreateAsync(PurchaseRequestBody request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ValidateReferencesAsync(request, cancellationToken);

        var now = clock.UtcNow;
        var purchase = new PurchaseRequest
        {
            Number = DocumentNumbers.Provisional(),
            RequestedById = Me,
            RequestedDate = DateOnly.FromDateTime(clock.Today)
        };
        Apply(purchase, request);

        await unitOfWork.PurchaseRequests.AddAsync(purchase, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        purchase.Number = await DocumentNumbers.FormatAsync(settings, clock, NumberPrefixKey, "PUR", purchase.Id, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(purchase.Id, cancellationToken);
    }

    public async Task<PurchaseRequestDto> UpdateAsync(int id, PurchaseRequestBody request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var purchase = await FindAsync(id, cancellationToken);
        EnsureRequesterOrManager(purchase);
        purchase.EnsureEditable();
        await ValidateReferencesAsync(request, cancellationToken);

        Apply(purchase, request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public Task<PurchaseRequestDto> SubmitAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, purchase =>
        {
            EnsureRequesterOrManager(purchase);
            purchase.Submit();
        }, cancellationToken);

    public Task<PurchaseRequestDto> ApproveAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, purchase => purchase.Approve(Me, clock.UtcNow), cancellationToken);

    public Task<PurchaseRequestDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default) =>
        MutateAsync(id, purchase => purchase.Reject(Me, clock.UtcNow, reason.Trim()), cancellationToken);

    public Task<PurchaseRequestDto> MarkOrderedAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, purchase => purchase.MarkOrdered(clock.UtcNow), cancellationToken);

    public Task<PurchaseRequestDto> MarkReceivedAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, purchase => purchase.MarkReceived(clock.UtcNow), cancellationToken);

    public Task<PurchaseRequestDto> CancelAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, purchase =>
        {
            // El solicitante puede cancelar hasta que se envie a aprobacion; despues, solo administracion de compras.
            if (!Has(Permissions.PurchasesManage)
                && (purchase.RequestedById != Me || purchase.Status is not (PurchaseStatus.Draft or PurchaseStatus.Submitted)))
            {
                throw new UnauthorizedAccessException("No tiene permiso para cancelar esta solicitud.");
            }

            purchase.Cancel(clock.UtcNow);
        }, cancellationToken);

    private int Me => currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado.");

    private bool Has(string permission) => currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<PurchaseRequestDto> MutateAsync(int id, Action<PurchaseRequest> mutation, CancellationToken cancellationToken)
    {
        var purchase = await FindAsync(id, cancellationToken);
        mutation(purchase);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<PurchaseRequest> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.PurchaseRequests.GetWithItemsAsync(id, cancellationToken) ?? throw new EntityNotFoundException("PurchaseRequest", id);

    private void EnsureRequesterOrManager(PurchaseRequest purchase)
    {
        if (purchase.RequestedById != Me && !Has(Permissions.PurchasesManage))
        {
            throw new UnauthorizedAccessException("Solo el solicitante o un administrador de compras puede hacerlo.");
        }
    }

    private async Task ValidateReferencesAsync(PurchaseRequestBody request, CancellationToken cancellationToken)
    {
        if (request.DepartmentId is { } dep && !await unitOfWork.Departments.ExistsAsync(dep, cancellationToken))
        {
            throw new DomainValidationException("DEPARTMENT_NOT_FOUND", "El departamento indicado no existe.");
        }

        var vendorIds = request.Items.Select(i => i.VendorId).Append(request.VendorId).OfType<int>().Distinct();
        foreach (var vendorId in vendorIds)
        {
            if (await unitOfWork.Vendors.GetByIdAsync(vendorId, cancellationToken) is not { Status: VendorStatus.Active })
            {
                throw new DomainValidationException("VENDOR_NOT_AVAILABLE", $"El proveedor {vendorId} no existe o no esta activo.");
            }
        }

        foreach (var typeId in request.Items.Select(i => i.AssetTypeId).OfType<int>().Distinct())
        {
            if (!await unitOfWork.AssetTypes.ExistsAsync(typeId, cancellationToken))
            {
                throw new DomainValidationException("ASSET_TYPE_NOT_FOUND", $"El tipo de activo {typeId} no existe.");
            }
        }
    }

    private static void Apply(PurchaseRequest purchase, PurchaseRequestBody request)
    {
        purchase.Title = request.Title.Trim();
        purchase.Description = Normalize(request.Description);
        purchase.DepartmentId = request.DepartmentId;
        purchase.VendorId = request.VendorId;
        purchase.Justification = Normalize(request.Justification);
        purchase.NeededDate = request.NeededDate;
        purchase.ReplaceItems(request.Items.Select(i => new PurchaseItem
        {
            Description = i.Description.Trim(),
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            AssetTypeId = i.AssetTypeId,
            VendorId = i.VendorId,
            Notes = Normalize(i.Notes)
        }));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
