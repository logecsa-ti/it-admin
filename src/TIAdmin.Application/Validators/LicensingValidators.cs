namespace TIAdmin.Application.Validators;

using FluentValidation;
using TIAdmin.Application.Common.Models;

public sealed class VendorRequestValidator : AbstractValidator<VendorRequest>
{
    public VendorRequestValidator()
    {
        RuleFor(x => x.Code).MaximumLength(20).WithMessage("El codigo no puede exceder 20 caracteres.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");
        RuleFor(x => x.TaxId).MaximumLength(50).WithMessage("El identificador fiscal no puede exceder 50 caracteres.");
        RuleFor(x => x.ContactName).MaximumLength(150).WithMessage("El contacto no puede exceder 150 caracteres.");
        RuleFor(x => x.Email)
            .MaximumLength(200).WithMessage("El correo no puede exceder 200 caracteres.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("El correo no es valido.");
        RuleFor(x => x.Phone).MaximumLength(30).WithMessage("El telefono no puede exceder 30 caracteres.");
        RuleFor(x => x.Address).MaximumLength(300).WithMessage("La direccion no puede exceder 300 caracteres.");
        RuleFor(x => x.City).MaximumLength(100).WithMessage("La ciudad no puede exceder 100 caracteres.");
        RuleFor(x => x.Country).MaximumLength(100).WithMessage("El pais no puede exceder 100 caracteres.");
        RuleFor(x => x.Website).MaximumLength(200).WithMessage("El sitio web no puede exceder 200 caracteres.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("El estado no es valido.");
        RuleFor(x => x.Notes).MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.");
        RuleFor(x => x.Rating)
            .InclusiveBetween(0, 5).When(x => x.Rating.HasValue).WithMessage("La calificacion debe estar entre 0 y 5.");
    }
}

public sealed class CreateContractRequestValidator : AbstractValidator<CreateContractRequest>
{
    public CreateContractRequestValidator()
    {
        RuleFor(x => x.Number)
            .NotEmpty().WithMessage("El numero de contrato es obligatorio.")
            .MaximumLength(50).WithMessage("El numero de contrato no puede exceder 50 caracteres.");

        Include(new ContractDataRules());
    }
}

public sealed class UpdateContractRequestValidator : AbstractValidator<UpdateContractRequest>
{
    public UpdateContractRequestValidator()
    {
        Include(new ContractDataRules());
    }
}

internal sealed class ContractDataRules : AbstractValidator<IContractData>
{
    public ContractDataRules()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");
        RuleFor(x => x.VendorId).GreaterThan(0).WithMessage("El proveedor es obligatorio.");
        RuleFor(x => x.Type).IsInEnum().WithMessage("El tipo de contrato no es valido.");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate).WithMessage("La fecha final debe ser posterior a la fecha inicial.");
        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0).When(x => x.Value.HasValue).WithMessage("El valor no puede ser negativo.");
        RuleFor(x => x.Currency)
            .Matches("^[A-Za-z]{3}$").When(x => !string.IsNullOrWhiteSpace(x.Currency))
            .WithMessage("La moneda debe ser un codigo ISO de 3 letras (ej. USD).");
        RuleFor(x => x.RenewalNoticeDays)
            .InclusiveBetween(0, 365).When(x => x.RenewalNoticeDays.HasValue)
            .WithMessage("El aviso de renovacion debe estar entre 0 y 365 dias.");
        RuleFor(x => x.ResponsibleUserId)
            .GreaterThan(0).When(x => x.ResponsibleUserId.HasValue).WithMessage("El responsable debe ser valido.");
        RuleFor(x => x.Notes).MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.");
    }
}

public sealed class RenewContractRequestValidator : AbstractValidator<RenewContractRequest>
{
    public RenewContractRequestValidator()
    {
        RuleFor(x => x.Number)
            .NotEmpty().WithMessage("El numero del nuevo contrato es obligatorio.")
            .MaximumLength(50).WithMessage("El numero de contrato no puede exceder 50 caracteres.");
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate).WithMessage("La fecha final debe ser posterior a la fecha inicial.");
        RuleFor(x => x.Value)
            .GreaterThanOrEqualTo(0).When(x => x.Value.HasValue).WithMessage("El valor no puede ser negativo.");
    }
}

public sealed class SoftwareRequestValidator : AbstractValidator<SoftwareRequest>
{
    public SoftwareRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");
        RuleFor(x => x.Version).MaximumLength(50).WithMessage("La version no puede exceder 50 caracteres.");
        RuleFor(x => x.Publisher).MaximumLength(150).WithMessage("El fabricante no puede exceder 150 caracteres.");
        RuleFor(x => x.Category).MaximumLength(100).WithMessage("La categoria no puede exceder 100 caracteres.");
        RuleFor(x => x.Description).MaximumLength(500).WithMessage("La descripcion no puede exceder 500 caracteres.");
    }
}

public sealed class CreateLicenseRequestValidator : AbstractValidator<CreateLicenseRequest>
{
    public CreateLicenseRequestValidator()
    {
        RuleFor(x => x.LicenseKey).MaximumLength(200).WithMessage("La clave no puede exceder 200 caracteres.");
        Include(new LicenseDataRules());
    }
}

public sealed class UpdateLicenseRequestValidator : AbstractValidator<UpdateLicenseRequest>
{
    public UpdateLicenseRequestValidator()
    {
        RuleFor(x => x.LicenseKey).MaximumLength(200).WithMessage("La clave no puede exceder 200 caracteres.");
        Include(new LicenseDataRules());
    }
}

internal sealed class LicenseDataRules : AbstractValidator<ILicenseData>
{
    public LicenseDataRules()
    {
        RuleFor(x => x.SoftwareId).GreaterThan(0).WithMessage("El software es obligatorio.");
        RuleFor(x => x.VendorId).GreaterThan(0).When(x => x.VendorId.HasValue).WithMessage("El proveedor debe ser valido.");
        RuleFor(x => x.ContractId).GreaterThan(0).When(x => x.ContractId.HasValue).WithMessage("El contrato debe ser valido.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder 150 caracteres.");
        RuleFor(x => x.LicenseType).IsInEnum().WithMessage("El tipo de licencia no es valido.");
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("La cantidad no puede ser negativa.");
        RuleFor(x => x.ExpirationDate)
            .GreaterThanOrEqualTo(x => x.PurchaseDate)
            .When(x => x.ExpirationDate.HasValue && x.PurchaseDate.HasValue)
            .WithMessage("La licencia no puede vencer antes de su fecha de compra.");
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue).WithMessage("El costo no puede ser negativo.");
        RuleFor(x => x.Notes).MaximumLength(1000).WithMessage("Las notas no pueden exceder 1000 caracteres.");
    }
}

public sealed class InstallLicenseRequestValidator : AbstractValidator<InstallLicenseRequest>
{
    public InstallLicenseRequestValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0).WithMessage("El activo es obligatorio.");
        RuleFor(x => x.UserId).GreaterThan(0).When(x => x.UserId.HasValue).WithMessage("El usuario debe ser valido.");
    }
}
