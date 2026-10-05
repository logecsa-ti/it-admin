namespace TIAdmin.Application.Validators;

using FluentValidation;
using TIAdmin.Application.Common.Models;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("El usuario es obligatorio.")
            .MaximumLength(256).WithMessage("El usuario no puede exceder 256 caracteres.")
            .Matches("^[a-zA-Z0-9._@-]+$").WithMessage("El usuario solo puede contener letras, numeros y . _ @ -");

        RuleFor(x => x.Password).PasswordPolicy("La contrasena");

        Include(new UserProfileRules());
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        Include(new UserProfileRules());
    }
}

/// <summary>Reglas comunes de los datos de perfil (alta y edicion).</summary>
internal sealed class UserProfileRules : AbstractValidator<IUserProfileData>
{
    public UserProfileRules()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo no puede exceder 256 caracteres.")
            .EmailAddress().WithMessage("El correo no es valido.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(100).WithMessage("El apellido no puede exceder 100 caracteres.");

        RuleFor(x => x.EmployeeCode)
            .MaximumLength(50).WithMessage("El codigo de empleado no puede exceder 50 caracteres.");

        RuleFor(x => x.JobTitle)
            .MaximumLength(100).WithMessage("El cargo no puede exceder 100 caracteres.");

        RuleFor(x => x.DepartmentId)
            .GreaterThan(0).When(x => x.DepartmentId.HasValue).WithMessage("El departamento debe ser valido.");

        RuleFor(x => x.LocationId)
            .GreaterThan(0).When(x => x.LocationId.HasValue).WithMessage("La ubicacion debe ser valida.");
    }
}

public sealed class AssignRolesRequestValidator : AbstractValidator<AssignRolesRequest>
{
    public AssignRolesRequestValidator()
    {
        RuleFor(x => x.Roles)
            .NotNull().WithMessage("La lista de roles es obligatoria.")
            .NotEmpty().WithMessage("El usuario debe tener al menos un rol.");

        RuleForEach(x => x.Roles)
            .NotEmpty().WithMessage("El nombre del rol no puede estar vacio.");
    }
}

public sealed class AssignPermissionsRequestValidator : AbstractValidator<AssignPermissionsRequest>
{
    public AssignPermissionsRequestValidator()
    {
        RuleFor(x => x.Permissions)
            .NotNull().WithMessage("La lista de permisos es obligatoria (puede estar vacia).");

        RuleForEach(x => x.Permissions)
            .NotEmpty().WithMessage("El codigo de permiso no puede estar vacio.");
    }
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).RoleName();

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripcion no puede exceder 500 caracteres.");

        RuleForEach(x => x.Permissions)
            .NotEmpty().WithMessage("El codigo de permiso no puede estar vacio.");
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).RoleName();

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripcion no puede exceder 500 caracteres.");
    }
}

internal static class RoleNameRules
{
    /// <summary>Mismo formato que los roles de sistema: MAYUSCULAS_CON_GUION_BAJO.</summary>
    public static IRuleBuilderOptions<T, string> RoleName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .Matches("^[A-Z][A-Z0-9_]{2,49}$")
            .WithMessage("El nombre del rol debe tener 3-50 caracteres en MAYUSCULAS, numeros o guion bajo (ej. TI_REPORTES).");
}
