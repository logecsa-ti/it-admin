namespace TIAdmin.Application.Validators;

using FluentValidation;
using TIAdmin.Application.Common.Models;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("El usuario es obligatorio.")
            .MaximumLength(256).WithMessage("El usuario no puede exceder 256 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contrasena es obligatoria.")
            .MaximumLength(128).WithMessage("La contrasena no puede exceder 128 caracteres.");
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("El refresh token es obligatorio.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("La contrasena actual es obligatoria.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La nueva contrasena es obligatoria.")
            .MinimumLength(10).WithMessage("La nueva contrasena debe tener al menos 10 caracteres.")
            .Matches("[A-Z]").WithMessage("La nueva contrasena debe incluir una letra mayuscula.")
            .Matches("[a-z]").WithMessage("La nueva contrasena debe incluir una letra minuscula.")
            .Matches("[0-9]").WithMessage("La nueva contrasena debe incluir un numero.")
            .Matches("[^a-zA-Z0-9]").WithMessage("La nueva contrasena debe incluir un caracter especial.")
            .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contrasena debe ser distinta de la actual.");
    }
}
