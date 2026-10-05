namespace TIAdmin.Application.Validators;

using FluentValidation;
using TIAdmin.Application.Common.Models;

public sealed class MaintenanceRequestValidator : AbstractValidator<MaintenanceRequest>
{
    public MaintenanceRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El titulo es obligatorio.")
            .MaximumLength(200).WithMessage("El titulo no puede exceder 200 caracteres.");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("La descripcion no puede exceder 4000 caracteres.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("El tipo de mantenimiento no es valido.");
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("El activo es obligatorio.");
        RuleFor(x => x.ScheduledDate).NotEmpty().WithMessage("La fecha programada es obligatoria.");
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0).When(x => x.EstimatedCost.HasValue).WithMessage("El costo estimado no puede ser negativo.");
        RuleFor(x => x.TechnicianId).GreaterThan(0).When(x => x.TechnicianId.HasValue).WithMessage("El tecnico debe ser valido.");
        RuleFor(x => x.VendorId).GreaterThan(0).When(x => x.VendorId.HasValue).WithMessage("El proveedor debe ser valido.");
        RuleFor(x => x.TicketId).GreaterThan(0).When(x => x.TicketId.HasValue).WithMessage("El ticket debe ser valido.");
        RuleFor(x => x.ContractId).GreaterThan(0).When(x => x.ContractId.HasValue).WithMessage("El contrato debe ser valido.");
    }
}

public sealed class CompleteMaintenanceRequestValidator : AbstractValidator<CompleteMaintenanceRequest>
{
    public CompleteMaintenanceRequestValidator()
    {
        RuleFor(x => x.Actions)
            .NotEmpty().WithMessage("Indique las acciones realizadas.")
            .MaximumLength(4000).WithMessage("Las acciones no pueden exceder 4000 caracteres.");
        RuleFor(x => x.Findings).MaximumLength(4000).WithMessage("Los hallazgos no pueden exceder 4000 caracteres.");
        RuleFor(x => x.Recommendations).MaximumLength(4000).WithMessage("Las recomendaciones no pueden exceder 4000 caracteres.");
        RuleFor(x => x.ActualCost).GreaterThanOrEqualTo(0).When(x => x.ActualCost.HasValue).WithMessage("El costo real no puede ser negativo.");
    }
}

/// <summary>Motivo obligatorio (cancelar, rechazar, rollback).</summary>
public sealed class CancelRequestValidator : AbstractValidator<CancelRequest>
{
    public CancelRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Indique el motivo.")
            .MaximumLength(1000).WithMessage("El motivo no puede exceder 1000 caracteres.");
    }
}

public sealed class ChangeRequestBodyValidator : AbstractValidator<ChangeRequestBody>
{
    public ChangeRequestBodyValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El titulo es obligatorio.")
            .MaximumLength(200).WithMessage("El titulo no puede exceder 200 caracteres.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripcion es obligatoria.")
            .MaximumLength(8000).WithMessage("La descripcion no puede exceder 8000 caracteres.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("El tipo de cambio no es valido.");
        RuleFor(x => x.Risk).IsInEnum().WithMessage("El riesgo no es valido.");
        RuleFor(x => x.Impact).IsInEnum().WithMessage("El impacto no es valido.");
        RuleFor(x => x.RollbackPlan).MaximumLength(8000).WithMessage("El plan de rollback no puede exceder 8000 caracteres.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId.HasValue).WithMessage("El departamento debe ser valido.");
        RuleFor(x => x.AssetId).GreaterThan(0).When(x => x.AssetId.HasValue).WithMessage("El activo debe ser valido.");
    }
}

public sealed class AssignUserRequestValidator : AbstractValidator<AssignUserRequest>
{
    public AssignUserRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("El usuario es obligatorio.");
    }
}

public sealed class PurchaseRequestBodyValidator : AbstractValidator<PurchaseRequestBody>
{
    public PurchaseRequestBodyValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El titulo es obligatorio.")
            .MaximumLength(200).WithMessage("El titulo no puede exceder 200 caracteres.");
        RuleFor(x => x.Description).MaximumLength(4000).WithMessage("La descripcion no puede exceder 4000 caracteres.");
        RuleFor(x => x.Justification).MaximumLength(4000).WithMessage("La justificacion no puede exceder 4000 caracteres.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId.HasValue).WithMessage("El departamento debe ser valido.");
        RuleFor(x => x.VendorId).GreaterThan(0).When(x => x.VendorId.HasValue).WithMessage("El proveedor debe ser valido.");
        RuleFor(x => x.Items).NotNull().WithMessage("La lista de partidas es obligatoria (puede estar vacia en borrador).");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Description)
                .NotEmpty().WithMessage("Cada partida requiere descripcion.")
                .MaximumLength(300).WithMessage("La descripcion de la partida no puede exceder 300 caracteres.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor que cero.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
            item.RuleFor(i => i.Notes).MaximumLength(500).WithMessage("Las notas de la partida no pueden exceder 500 caracteres.");
        });
    }
}
