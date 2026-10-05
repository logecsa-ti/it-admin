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
            .PasswordPolicy("La nueva contrasena")
            .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contrasena debe ser distinta de la actual.");
    }
}

public static class PasswordPolicyRules
{
    /// <summary>
    /// Replica la politica de Identity (ServiceCollectionExtensions.AddIdentity) con mensajes en espanol,
    /// para que los errores en ingles de Identity no lleguen al usuario.
    /// </summary>
    public static IRuleBuilderOptions<T, string> PasswordPolicy<T>(this IRuleBuilder<T, string> rule, string label) =>
        rule
            .NotEmpty().WithMessage($"{label} es obligatoria.")
            .MinimumLength(10).WithMessage($"{label} debe tener al menos 10 caracteres.")
            .MaximumLength(128).WithMessage($"{label} no puede exceder 128 caracteres.")
            .Matches("[A-Z]").WithMessage($"{label} debe incluir una letra mayuscula.")
            .Matches("[a-z]").WithMessage($"{label} debe incluir una letra minuscula.")
            .Matches("[0-9]").WithMessage($"{label} debe incluir un numero.")
            .Matches("[^a-zA-Z0-9]").WithMessage($"{label} debe incluir un caracter especial.");
}
