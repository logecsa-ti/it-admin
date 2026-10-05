namespace TIAdmin.Application.Validators;

using FluentValidation;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Enums;

public sealed class CreateAssetRequestValidator : AbstractValidator<CreateAssetRequest>
{
    public CreateAssetRequestValidator()
    {
        RuleFor(x => x.AssetCode)
            .NotEmpty().WithMessage("El codigo de activo es obligatorio.")
            .MaximumLength(30).WithMessage("El codigo de activo no puede exceder 30 caracteres.")
            .Matches("^[A-Za-z0-9._/-]+$").WithMessage("El codigo de activo solo puede contener letras, numeros y . _ / -");

        Include(new AssetDataRules());
    }
}

public sealed class UpdateAssetRequestValidator : AbstractValidator<UpdateAssetRequest>
{
    public UpdateAssetRequestValidator()
    {
        Include(new AssetDataRules());
    }
}

/// <summary>Reglas comunes de alta y edicion de activos.</summary>
internal sealed class AssetDataRules : AbstractValidator<IAssetData>
{
    public AssetDataRules()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");

        RuleFor(x => x.SerialNumber)
            .MaximumLength(100).WithMessage("El numero de serie no puede exceder 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripcion no puede exceder 500 caracteres.");

        RuleFor(x => x.AssetTypeId)
            .GreaterThan(0).WithMessage("El tipo de activo es obligatorio.");

        RuleFor(x => x.Brand)
            .MaximumLength(100).WithMessage("La marca no puede exceder 100 caracteres.");

        RuleFor(x => x.Model)
            .MaximumLength(100).WithMessage("El modelo no puede exceder 100 caracteres.");

        RuleFor(x => x.PurchaseCost)
            .GreaterThanOrEqualTo(0).When(x => x.PurchaseCost.HasValue)
            .WithMessage("El costo de compra no puede ser negativo.");

        RuleFor(x => x.WarrantyExpiration)
            .GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.WarrantyExpiration.HasValue && x.PurchaseDate.HasValue)
            .WithMessage("La garantia no puede vencer antes de la fecha de compra.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.");

        RuleFor(x => x.LocationId)
            .GreaterThan(0).When(x => x.LocationId.HasValue).WithMessage("La ubicacion debe ser valida.");

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).When(x => x.DepartmentId.HasValue).WithMessage("El departamento debe ser valido.");

        RuleFor(x => x.VendorId)
            .GreaterThan(0).When(x => x.VendorId.HasValue).WithMessage("El proveedor debe ser valido.");

        RuleFor(x => x.ParentAssetId)
            .GreaterThan(0).When(x => x.ParentAssetId.HasValue).WithMessage("El activo padre debe ser valido.");
    }
}

public sealed class ChangeAssetStatusRequestValidator : AbstractValidator<ChangeAssetStatusRequest>
{
    public ChangeAssetStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("El estado no es valido.")
            .NotEqual(AssetStatus.Assigned).WithMessage("Para asignar un activo use POST /assets/{id}/assign.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.");
    }
}

public sealed class AssignAssetRequestValidator : AbstractValidator<AssignAssetRequest>
{
    public AssignAssetRequestValidator()
    {
        RuleFor(x => x.UserId)
            .GreaterThan(0).WithMessage("El usuario es obligatorio.");

        RuleFor(x => x.Condition)
            .MaximumLength(500).WithMessage("La condicion no puede exceder 500 caracteres.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.");
    }
}

public sealed class ReturnAssetRequestValidator : AbstractValidator<ReturnAssetRequest>
{
    public ReturnAssetRequestValidator()
    {
        RuleFor(x => x.Condition)
            .MaximumLength(500).WithMessage("La condicion no puede exceder 500 caracteres.");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.");

        RuleFor(x => x.ResultingStatus)
            .Must(s => s is null or AssetStatus.Available or AssetStatus.Maintenance or AssetStatus.Repair)
            .WithMessage("Al devolver un activo solo puede quedar Available, Maintenance o Repair.");
    }
}

public sealed class CreateAssetTypeRequestValidator : AbstractValidator<CreateAssetTypeRequest>
{
    public CreateAssetTypeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("El codigo es obligatorio.")
            .Matches("^[A-Z][A-Z0-9_]{1,29}$").WithMessage("El codigo debe tener 2-30 caracteres en MAYUSCULAS, numeros o guion bajo.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("La descripcion no puede exceder 300 caracteres.");
    }
}

public sealed class UpdateAssetTypeRequestValidator : AbstractValidator<UpdateAssetTypeRequest>
{
    public UpdateAssetTypeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("La descripcion no puede exceder 300 caracteres.");
    }
}
