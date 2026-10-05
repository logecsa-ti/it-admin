namespace TIAdmin.Application.Operations;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Gestion de cambios (SPECS.md seccion 27). Revisar/aprobar: CHANGES.REVIEW (nunca el solicitante).
/// Implementar: el responsable asignado o CHANGES.MANAGE. Editar/enviar el borrador: el solicitante o CHANGES.MANAGE.
/// </summary>
public interface IChangeService
{
    Task<PagedResult<ChangeRequestDto>> SearchAsync(PagedQuery query, ChangeFilter filter, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> CreateAsync(ChangeRequestBody request, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> UpdateAsync(int id, ChangeRequestBody request, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> SubmitAsync(int id, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> StartReviewAsync(int id, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> ApproveAsync(int id, string? comment, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> AssignAsync(int id, int userId, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> StartImplementationAsync(int id, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> CompleteAsync(int id, string? notes, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> RollBackAsync(int id, string notes, CancellationToken cancellationToken = default);

    Task<ChangeRequestDto> CloseAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Solo borradores.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class ChangeService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IUserDirectory userDirectory,
    ISystemSettings settings,
    IClock clock)
    : IChangeService
{
    public const string NumberPrefixKey = "Changes.NumberPrefix";

    public Task<PagedResult<ChangeRequestDto>> SearchAsync(PagedQuery query, ChangeFilter filter, CancellationToken cancellationToken = default) =>
        unitOfWork.ChangeRequests.SearchAsync(query, filter, Me, cancellationToken);

    public async Task<ChangeRequestDto> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await unitOfWork.ChangeRequests.GetDtoAsync(id, cancellationToken) ?? throw new EntityNotFoundException("ChangeRequest", id);

    public async Task<ChangeRequestDto> CreateAsync(ChangeRequestBody request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await ValidateReferencesAsync(request, cancellationToken);

        var now = clock.UtcNow;
        var change = new ChangeRequest { Number = DocumentNumbers.Provisional(), RequestedById = Me };
        Apply(change, request);

        await unitOfWork.ChangeRequests.AddAsync(change, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        change.Number = await DocumentNumbers.FormatAsync(settings, clock, NumberPrefixKey, "CHG", change.Id, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(change.Id, cancellationToken);
    }

    public async Task<ChangeRequestDto> UpdateAsync(int id, ChangeRequestBody request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var change = await FindAsync(id, cancellationToken);
        EnsureRequesterOrManager(change);
        change.EnsureEditable();
        await ValidateReferencesAsync(request, cancellationToken);

        Apply(change, request);
        unitOfWork.ChangeRequests.Update(change);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public Task<ChangeRequestDto> SubmitAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change =>
        {
            EnsureRequesterOrManager(change);
            change.Submit(clock.UtcNow);
        }, cancellationToken);

    public Task<ChangeRequestDto> StartReviewAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change => change.StartReview(), cancellationToken);

    public Task<ChangeRequestDto> ApproveAsync(int id, string? comment, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change => change.Approve(Me, clock.UtcNow, Normalize(comment)), cancellationToken);

    public Task<ChangeRequestDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change => change.Reject(Me, clock.UtcNow, reason.Trim()), cancellationToken);

    public async Task<ChangeRequestDto> AssignAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        if (await userDirectory.FindAsync(userId, cancellationToken) is not { IsActive: true })
        {
            throw new DomainValidationException("USER_NOT_AVAILABLE", "El usuario indicado no existe o esta inactivo.");
        }

        return await MutateAsync(id, change => change.Assign(userId), cancellationToken);
    }

    public Task<ChangeRequestDto> StartImplementationAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change =>
        {
            EnsureImplementer(change);
            change.StartImplementation(clock.UtcNow);
        }, cancellationToken);

    public Task<ChangeRequestDto> CompleteAsync(int id, string? notes, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change =>
        {
            EnsureImplementer(change);
            change.Complete(clock.UtcNow, Normalize(notes));
        }, cancellationToken);

    public Task<ChangeRequestDto> RollBackAsync(int id, string notes, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change =>
        {
            EnsureImplementer(change);
            change.RollBack(clock.UtcNow, notes);
        }, cancellationToken);

    public Task<ChangeRequestDto> CloseAsync(int id, CancellationToken cancellationToken = default) =>
        MutateAsync(id, change => change.Close(clock.UtcNow), cancellationToken);

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var change = await FindAsync(id, cancellationToken);
        EnsureRequesterOrManager(change);
        change.EnsureEditable();

        unitOfWork.ChangeRequests.Delete(change);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private int Me => currentUser.UserId ?? throw new UnauthorizedAccessException("Operacion sin usuario autenticado.");

    private bool Has(string permission) => currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<ChangeRequestDto> MutateAsync(int id, Action<ChangeRequest> mutation, CancellationToken cancellationToken)
    {
        var change = await FindAsync(id, cancellationToken);
        mutation(change);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<ChangeRequest> FindAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.ChangeRequests.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("ChangeRequest", id);

    private void EnsureRequesterOrManager(ChangeRequest change)
    {
        if (change.RequestedById != Me && !Has(Permissions.ChangesManage))
        {
            throw new UnauthorizedAccessException("Solo el solicitante o un administrador de cambios puede hacerlo.");
        }
    }

    private void EnsureImplementer(ChangeRequest change)
    {
        if (change.AssignedToId != Me && !Has(Permissions.ChangesManage))
        {
            throw new UnauthorizedAccessException("Solo el responsable asignado o un administrador de cambios puede implementar.");
        }
    }

    private async Task ValidateReferencesAsync(ChangeRequestBody request, CancellationToken cancellationToken)
    {
        if (request.DepartmentId is { } dep && !await unitOfWork.Departments.ExistsAsync(dep, cancellationToken))
        {
            throw new DomainValidationException("DEPARTMENT_NOT_FOUND", "El departamento indicado no existe.");
        }

        if (request.AssetId is { } asset && !await unitOfWork.Assets.ExistsAsync(asset, cancellationToken))
        {
            throw new DomainValidationException("ASSET_NOT_FOUND", "El activo indicado no existe.");
        }
    }

    private static void Apply(ChangeRequest change, ChangeRequestBody request)
    {
        change.Title = request.Title.Trim();
        change.Description = request.Description.Trim();
        change.Type = request.Type;
        change.Risk = request.Risk;
        change.Impact = request.Impact;
        change.DepartmentId = request.DepartmentId;
        change.AssetId = request.AssetId;
        change.PlannedDate = request.PlannedDate is { } planned ? DateTime.SpecifyKind(planned, DateTimeKind.Utc) : null;
        change.RollbackPlan = Normalize(request.RollbackPlan);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
