namespace TIAdmin.Application.Validators;

using FluentValidation;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Enums;

public sealed class TicketCategoryRequestValidator : AbstractValidator<TicketCategoryRequest>
{
    public TicketCategoryRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El codigo es obligatorio.")
            .Matches("^[A-Za-z][A-Za-z0-9_]{1,19}$").WithMessage("El codigo debe tener 2-20 letras, numeros o guion bajo.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");
        RuleFor(x => x.Description).MaximumLength(300).WithMessage("La descripcion no puede exceder 300 caracteres.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("El tipo no es valido.");
        RuleFor(x => x.DefaultPriority).IsInEnum().WithMessage("La prioridad no es valida.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId.HasValue).WithMessage("El departamento debe ser valido.");
    }
}

public sealed class SlaPolicyRequestValidator : AbstractValidator<SlaPolicyRequest>
{
    public SlaPolicyRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue).WithMessage("La prioridad no es valida.");
        RuleFor(x => x.TicketType).IsInEnum().When(x => x.TicketType.HasValue).WithMessage("El tipo no es valido.");
        RuleFor(x => x.ResponseTimeMinutes)
            .GreaterThan(0).WithMessage("El tiempo de respuesta debe ser mayor que cero.");
        RuleFor(x => x.ResolutionTimeMinutes)
            .GreaterThanOrEqualTo(x => x.ResponseTimeMinutes).WithMessage("El tiempo de resolucion no puede ser menor que el de respuesta.");
        RuleFor(x => x.WorkEndTime)
            .GreaterThan(x => x.WorkStartTime)
            .When(x => x.WorkStartTime.HasValue && x.WorkEndTime.HasValue)
            .WithMessage("La hora de fin debe ser posterior a la de inicio.");
        RuleFor(x => x.WorkDays)
            .Matches("^\\s*[1-7](\\s*,\\s*[1-7])*\\s*$").When(x => !string.IsNullOrWhiteSpace(x.WorkDays))
            .WithMessage("Los dias laborables son numeros 1 (lunes) a 7 (domingo) separados por coma.");
    }
}

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El titulo es obligatorio.")
            .MaximumLength(200).WithMessage("El titulo no puede exceder 200 caracteres.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripcion es obligatoria.")
            .MaximumLength(8000).WithMessage("La descripcion no puede exceder 8000 caracteres.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("La categoria es obligatoria.");
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority.HasValue).WithMessage("La prioridad no es valida.");
        RuleFor(x => x.RequesterId).GreaterThan(0).When(x => x.RequesterId.HasValue).WithMessage("El solicitante debe ser valido.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId.HasValue).WithMessage("El departamento debe ser valido.");
        RuleFor(x => x.AssetId).GreaterThan(0).When(x => x.AssetId.HasValue).WithMessage("El activo debe ser valido.");
    }
}

public sealed class UpdateTicketRequestValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El titulo es obligatorio.")
            .MaximumLength(200).WithMessage("El titulo no puede exceder 200 caracteres.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripcion es obligatoria.")
            .MaximumLength(8000).WithMessage("La descripcion no puede exceder 8000 caracteres.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("La categoria es obligatoria.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("La prioridad no es valida.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).When(x => x.DepartmentId.HasValue).WithMessage("El departamento debe ser valido.");
        RuleFor(x => x.AssetId).GreaterThan(0).When(x => x.AssetId.HasValue).WithMessage("El activo debe ser valido.");
    }
}

public sealed class ChangeTicketStatusRequestValidator : AbstractValidator<ChangeTicketStatusRequest>
{
    public ChangeTicketStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("El estado no es valido.");
        RuleFor(x => x.Comment).MaximumLength(500).WithMessage("El comentario no puede exceder 500 caracteres.");
        RuleFor(x => x.ResolutionNotes)
            .NotEmpty().When(x => x.Status == TicketStatus.Resolved).WithMessage("Para resolver un ticket debe indicar la solucion.")
            .MaximumLength(4000).WithMessage("La solucion no puede exceder 4000 caracteres.");
    }
}

public sealed class AssignTicketRequestValidator : AbstractValidator<AssignTicketRequest>
{
    public AssignTicketRequestValidator()
    {
        RuleFor(x => x.AssignedToId).GreaterThan(0).WithMessage("El responsable es obligatorio.");
    }
}

public sealed class RejectRequestValidator : AbstractValidator<ApprovalDecisionRequest>
{
    public RejectRequestValidator()
    {
        RuleFor(x => x.Comment)
            .NotEmpty().WithMessage("Indique el motivo del rechazo.")
            .MaximumLength(500).WithMessage("El motivo no puede exceder 500 caracteres.");
    }
}

public sealed class AddTicketCommentRequestValidator : AbstractValidator<AddTicketCommentRequest>
{
    public AddTicketCommentRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("El comentario no puede estar vacio.")
            .MaximumLength(4000).WithMessage("El comentario no puede exceder 4000 caracteres.");
    }
}
