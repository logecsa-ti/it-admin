namespace TIAdmin.Application.Validators;

using FluentValidation;
using TIAdmin.Application.Common.Models;

public sealed class CreateAssetRequestValidator : AbstractValidator<CreateAssetRequest>
{
    public CreateAssetRequestValidator()
    {
        RuleFor(x => x.AssetCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AssetTypeId).GreaterThan(0);
    }
}
