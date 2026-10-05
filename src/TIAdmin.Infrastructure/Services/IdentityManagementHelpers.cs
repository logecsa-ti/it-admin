namespace TIAdmin.Infrastructure.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Domain.Exceptions;
using TIAdmin.Infrastructure.Persistence;

/// <summary>
/// Utilidades compartidas por los servicios de administracion de usuarios y roles.
/// </summary>
internal static class IdentityManagementHelpers
{
    /// <summary>
    /// Convierte los errores de Identity en excepciones de dominio: duplicados → 409, el resto → 400.
    /// Las reglas de contrasena ya se validan antes con mensajes en espanol (PasswordPolicyRules).
    /// </summary>
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        var duplicate = result.Errors.FirstOrDefault(e => e.Code is "DuplicateUserName" or "DuplicateEmail" or "DuplicateRoleName");
        if (duplicate is not null)
        {
            var (code, message) = duplicate.Code switch
            {
                "DuplicateUserName" => ("USERNAME_ALREADY_EXISTS", "El nombre de usuario ya existe."),
                "DuplicateEmail" => ("EMAIL_ALREADY_EXISTS", "El correo ya esta registrado."),
                _ => ("ROLE_ALREADY_EXISTS", "El rol ya existe.")
            };
            throw new ConflictException(code, message);
        }

        throw new DomainValidationException("IDENTITY_VALIDATION",
            string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>
    /// Traduce codigos de permiso a Ids. Lanza 400 con los codigos desconocidos.
    /// </summary>
    public static async Task<List<int>> ResolvePermissionIdsAsync(
        this TIAdminDbContext context,
        IEnumerable<string> codes,
        CancellationToken cancellationToken)
    {
        var requested = codes.Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        if (requested.Count == 0)
        {
            return [];
        }

        var found = await context.Permissions
            .Where(p => requested.Contains(p.Code))
            .Select(p => new { p.Id, p.Code })
            .ToListAsync(cancellationToken);

        var unknown = requested.Except(found.Select(p => p.Code), StringComparer.OrdinalIgnoreCase).ToList();
        if (unknown.Count > 0)
        {
            throw new DomainValidationException("PERMISSION_NOT_FOUND",
                $"Permisos inexistentes: {string.Join(", ", unknown)}.");
        }

        return found.Select(p => p.Id).ToList();
    }
}
