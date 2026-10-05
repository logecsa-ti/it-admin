namespace TIAdmin.Application.HelpDesk;

using TIAdmin.Application.Common;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Exceptions;

/// <summary>Configuracion del Help Desk: categorias de ticket y politicas de SLA (SPECS.md seccion 22).</summary>
public interface IHelpDeskConfigService
{
    Task<IReadOnlyList<TicketCategoryDto>> GetCategoriesAsync(bool? isActive, CancellationToken cancellationToken = default);

    Task<TicketCategoryDto> CreateCategoryAsync(TicketCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>El codigo y el tipo no cambian: los tickets existentes dependen de ellos.</summary>
    Task<TicketCategoryDto> UpdateCategoryAsync(int id, TicketCategoryRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SlaPolicyDto>> GetSlaPoliciesAsync(CancellationToken cancellationToken = default);

    Task<SlaPolicyDto> CreateSlaPolicyAsync(SlaPolicyRequest request, CancellationToken cancellationToken = default);

    Task<SlaPolicyDto> UpdateSlaPolicyAsync(int id, SlaPolicyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Baja logica. La politica por defecto no se elimina (siempre debe existir una).</summary>
    Task DeleteSlaPolicyAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class HelpDeskConfigService(IUnitOfWork unitOfWork, CatalogCache cache) : IHelpDeskConfigService
{
    private const string CategoriesCatalog = "ticket-categories";

    public Task<IReadOnlyList<TicketCategoryDto>> GetCategoriesAsync(bool? isActive, CancellationToken cancellationToken = default) =>
        cache.GetOrCreateAsync(CategoriesCatalog, isActive?.ToString() ?? "all",
            () => unitOfWork.TicketCategories.ListAsync(isActive, cancellationToken));

    public async Task<TicketCategoryDto> CreateCategoryAsync(TicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await unitOfWork.TicketCategories.ExistsCodeAsync(code, cancellationToken))
        {
            throw new ConflictException("CATEGORY_CODE_ALREADY_EXISTS", $"La categoria {code} ya existe.");
        }

        await EnsureCategoryNameAvailableAsync(request.Name.Trim(), null, cancellationToken);
        await EnsureDepartmentAsync(request.DepartmentId, cancellationToken);

        var category = new TicketCategory { Code = code, Type = request.Type };
        Apply(category, request);

        await unitOfWork.TicketCategories.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate(CategoriesCatalog);
        return ToDto(category);
    }

    public async Task<TicketCategoryDto> UpdateCategoryAsync(int id, TicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await unitOfWork.TicketCategories.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("TicketCategory", id);

        if (request.Type != category.Type)
        {
            throw new DomainValidationException("CATEGORY_TYPE_IMMUTABLE",
                "El tipo de la categoria (incidente o solicitud) no puede cambiar; cree una categoria nueva.");
        }

        await EnsureCategoryNameAvailableAsync(request.Name.Trim(), id, cancellationToken);
        await EnsureDepartmentAsync(request.DepartmentId, cancellationToken);

        Apply(category, request);
        unitOfWork.TicketCategories.Update(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        cache.Invalidate(CategoriesCatalog);
        return ToDto(category);
    }

    public async Task<IReadOnlyList<SlaPolicyDto>> GetSlaPoliciesAsync(CancellationToken cancellationToken = default) =>
        (await unitOfWork.SlaPolicies.GetAllAsync(cancellationToken))
            .OrderByDescending(p => p.IsDefault)
            .ThenByDescending(p => p.Specificity)
            .ThenBy(p => p.Name)
            .Select(ToDto)
            .ToList();

    public async Task<SlaPolicyDto> CreateSlaPolicyAsync(SlaPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await ValidateSlaAsync(request, null, cancellationToken);

        var policy = new SlaPolicy();
        Apply(policy, request);

        await unitOfWork.SlaPolicies.AddAsync(policy, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (policy.IsDefault)
        {
            await unitOfWork.SlaPolicies.ClearDefaultAsync(policy.Id, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToDto(policy);
    }

    public async Task<SlaPolicyDto> UpdateSlaPolicyAsync(int id, SlaPolicyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var policy = await unitOfWork.SlaPolicies.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("SlaPolicy", id);

        if (policy.IsDefault && !request.IsDefault)
        {
            throw new ConflictException("SLA_DEFAULT_REQUIRED", "Marque otra politica como predeterminada antes de quitar esta.");
        }

        await ValidateSlaAsync(request, id, cancellationToken);

        Apply(policy, request);
        if (policy.IsDefault)
        {
            await unitOfWork.SlaPolicies.ClearDefaultAsync(policy.Id, cancellationToken);
        }

        unitOfWork.SlaPolicies.Update(policy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(policy);
    }

    public async Task DeleteSlaPolicyAsync(int id, CancellationToken cancellationToken = default)
    {
        var policy = await unitOfWork.SlaPolicies.GetByIdAsync(id, cancellationToken)
            ?? throw new EntityNotFoundException("SlaPolicy", id);

        if (policy.IsDefault)
        {
            throw new ConflictException("SLA_DEFAULT_REQUIRED", "La politica predeterminada no puede eliminarse.");
        }

        unitOfWork.SlaPolicies.Delete(policy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateSlaAsync(SlaPolicyRequest request, int? excludeId, CancellationToken cancellationToken)
    {
        if (await unitOfWork.SlaPolicies.ExistsNameAsync(request.Name.Trim(), excludeId, cancellationToken))
        {
            throw new ConflictException("SLA_NAME_ALREADY_EXISTS", $"Ya existe una politica de SLA llamada {request.Name.Trim()}.");
        }

        if (request.IsDefault && (request.CategoryId is not null || request.Priority is not null
            || request.TicketType is not null || request.DepartmentId is not null))
        {
            throw new DomainValidationException("SLA_DEFAULT_WITH_CRITERIA",
                "La politica predeterminada no puede tener criterios (aplica cuando ninguna otra coincide).");
        }

        if (request.CategoryId is { } categoryId && !await unitOfWork.TicketCategories.ExistsAsync(categoryId, cancellationToken))
        {
            throw new DomainValidationException("CATEGORY_NOT_FOUND", "La categoria indicada no existe.");
        }

        await EnsureDepartmentAsync(request.DepartmentId, cancellationToken);
    }

    private async Task EnsureCategoryNameAvailableAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        if (await unitOfWork.TicketCategories.ExistsNameAsync(name, excludeId, cancellationToken))
        {
            throw new ConflictException("CATEGORY_NAME_ALREADY_EXISTS", $"Ya existe una categoria llamada {name}.");
        }
    }

    private async Task EnsureDepartmentAsync(int? departmentId, CancellationToken cancellationToken)
    {
        if (departmentId is { } dep && !await unitOfWork.Departments.ExistsAsync(dep, cancellationToken))
        {
            throw new DomainValidationException("DEPARTMENT_NOT_FOUND", "El departamento indicado no existe.");
        }
    }

    private static void Apply(TicketCategory category, TicketCategoryRequest request)
    {
        category.Name = request.Name.Trim();
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.DepartmentId = request.DepartmentId;
        category.DefaultPriority = request.DefaultPriority;
        category.RequiresApproval = request.Type == Domain.Enums.TicketType.ServiceRequest && request.RequiresApproval;
        category.IsActive = request.IsActive;
    }

    private static void Apply(SlaPolicy policy, SlaPolicyRequest request)
    {
        policy.Name = request.Name.Trim();
        policy.CategoryId = request.CategoryId;
        policy.Priority = request.Priority;
        policy.TicketType = request.TicketType;
        policy.DepartmentId = request.DepartmentId;
        policy.ResponseTimeMinutes = request.ResponseTimeMinutes;
        policy.ResolutionTimeMinutes = request.ResolutionTimeMinutes;
        policy.BusinessHoursOnly = request.BusinessHoursOnly;
        policy.WorkStartTime = request.WorkStartTime ?? new TimeOnly(8, 0);
        policy.WorkEndTime = request.WorkEndTime ?? new TimeOnly(17, 0);
        policy.WorkDays = string.IsNullOrWhiteSpace(request.WorkDays) ? "1,2,3,4,5" : request.WorkDays.Replace(" ", string.Empty, StringComparison.Ordinal);
        policy.IsDefault = request.IsDefault;
        policy.IsActive = request.IsDefault || request.IsActive;
    }

    private static TicketCategoryDto ToDto(TicketCategory c) =>
        new(c.Id, c.Code, c.Name, c.Description, c.Type, c.DepartmentId, c.DefaultPriority, c.RequiresApproval, c.IsActive);

    private static SlaPolicyDto ToDto(SlaPolicy p) =>
        new(p.Id, p.Name, p.CategoryId, p.Priority, p.TicketType, p.DepartmentId, p.ResponseTimeMinutes, p.ResolutionTimeMinutes,
            p.BusinessHoursOnly, p.WorkStartTime, p.WorkEndTime, p.WorkDays, p.IsDefault, p.IsActive);
}
