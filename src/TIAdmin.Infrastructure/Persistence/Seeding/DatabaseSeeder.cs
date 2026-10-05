namespace TIAdmin.Infrastructure.Persistence.Seeding;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Infrastructure.Identity;

/// <summary>
/// Datos iniciales: permisos, roles, tipos de activo, categorias de ticket,
/// configuracion del sistema y usuario administrador (solo desarrollo).
/// </summary>
public sealed class DatabaseSeeder(
    TIAdminDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOptions<SeedOptions> seedOptions,
    ILogger<DatabaseSeeder> logger)
{
    private readonly SeedOptions options = seedOptions.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await SeedPermissionsAsync(cancellationToken).ConfigureAwait(false);
        await SeedRolesAsync(cancellationToken).ConfigureAwait(false);
        await SeedAssetTypesAsync(cancellationToken).ConfigureAwait(false);
        await SeedConfigurationsAsync(cancellationToken).ConfigureAwait(false);

        if (options.Enabled)
        {
            await SeedAdminUserAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            logger.LogInformation("Seed de usuario administrador deshabilitado (Seed:Enabled=false).");
        }
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existing = await context.Permissions
            .Select(p => p.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toInsert = Permissions.All
            .Where(p => !existing.Contains(p.Code, StringComparer.OrdinalIgnoreCase))
            .Select(p => new ApplicationPermission
            {
                Code = p.Code,
                Module = p.Module,
                Action = p.Action,
                Description = p.Description
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            context.Permissions.AddRange(toInsert);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Seed: {Count} permisos insertados.", toInsert.Count);
        }

        var byCode = await context.Permissions
            .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);
        _permissionIdsByCode = byCode;
    }

    private Dictionary<string, int> _permissionIdsByCode = new(StringComparer.OrdinalIgnoreCase);

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in SystemRoles.All)
        {
            var role = await roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
            var created = false;
            if (role is null)
            {
                role = new ApplicationRole(roleName, SystemRoles.Descriptions.GetValueOrDefault(roleName))
                {
                    IsSystemRole = true
                };
                var result = await roleManager.CreateAsync(role).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"No se pudo crear el rol {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                created = true;
                logger.LogInformation("Seed: rol {Role} creado.", roleName);
            }
            else if (!role.IsSystemRole)
            {
                role.IsSystemRole = true;
                await roleManager.UpdateAsync(role).ConfigureAwait(false);
            }

            // La matriz por defecto solo se aplica al crear el rol: despues, sus permisos se
            // administran via /api/v1/roles y el seed no debe deshacer esos cambios.
            // SUPER_ADMIN es la excepcion: siempre recibe todo el catalogo (incluidos permisos nuevos).
            if (!created && !string.Equals(roleName, SystemRoles.SuperAdmin, StringComparison.Ordinal))
            {
                continue;
            }

            var desired = Permissions.ForRole(roleName);
            var permissionIds = desired
                .Where(_permissionIdsByCode.ContainsKey)
                .Select(code => _permissionIdsByCode[code])
                .ToList();

            var current = await context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var toAdd = permissionIds.Where(id => !current.Contains(id)).ToList();
            if (toAdd.Count > 0)
            {
                context.RolePermissions.AddRange(toAdd.Select(id => new ApplicationRolePermission
                {
                    RoleId = role.Id,
                    PermissionId = id
                }));
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Seed: {Count} permisos asignados al rol {Role}.", toAdd.Count, roleName);
            }
        }
    }

    private async Task SeedAssetTypesAsync(CancellationToken cancellationToken)
    {
        var defaults = new (string Code, string Name)[]
        {
            ("LAPTOP", "Laptop"),
            ("DESKTOP", "Desktop"),
            ("MONITOR", "Monitor"),
            ("PRINTER", "Printer"),
            ("SERVER", "Server"),
            ("NETWORK_DEVICE", "Network Device"),
            ("MOBILE_DEVICE", "Mobile Device"),
            ("UPS", "UPS"),
            ("STORAGE", "Storage"),
            ("TELEPHONE", "Telephone"),
            ("PERIPHERAL", "Peripheral"),
            ("OTHER", "Other")
        };

        var existing = await context.AssetTypes
            .Select(a => a.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toInsert = defaults
            .Where(d => !existing.Contains(d.Code, StringComparer.OrdinalIgnoreCase))
            .Select(d => new AssetType { Code = d.Code, Name = d.Name })
            .ToList();

        if (toInsert.Count > 0)
        {
            context.AssetTypes.AddRange(toInsert);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Seed: {Count} tipos de activo insertados.", toInsert.Count);
        }
    }

    private async Task SeedConfigurationsAsync(CancellationToken cancellationToken)
    {
        var defaults = new (string Key, string? Value, string Group, ConfigurationDataType Type, bool IsPublic, string Description)[]
        {
            ("App.TimeZone", "America/Managua", "Localization", ConfigurationDataType.String, true, "Zona horaria de presentacion"),
            ("App.Name", "TI Admin", "General", ConfigurationDataType.String, true, "Nombre visible de la aplicacion"),
            ("App.PageSize", "25", "General", ConfigurationDataType.Int, true, "Tamano de pagina por defecto"),
            ("App.MaxPageSize", "200", "General", ConfigurationDataType.Int, true, "Tamano maximo de pagina"),
            ("Sla.Default.ResponseMinutes", "240", "Sla", ConfigurationDataType.Int, true, "SLA de respuesta por defecto en minutos"),
            ("Sla.Default.ResolutionMinutes", "1440", "Sla", ConfigurationDataType.Int, true, "SLA de resolucion por defecto en minutos"),
            ("Alerts.Contract.Days", "90,60,30,15,7", "Alerts", ConfigurationDataType.String, true, "Dias de anticipacion para alertas de contratos"),
            ("Alerts.License.Days", "90,30,14,7", "Alerts", ConfigurationDataType.String, true, "Dias de anticipacion para alertas de licencias"),
            ("Alerts.Maintenance.Days", "7", "Alerts", ConfigurationDataType.Int, true, "Dias de anticipacion para alertas de mantenimiento")
        };

        var existing = await context.SystemConfigurations
            .Select(c => c.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toInsert = defaults
            .Where(d => !existing.Contains(d.Key, StringComparer.OrdinalIgnoreCase))
            .Select(d => new SystemConfiguration
            {
                Key = d.Key,
                Value = d.Value,
                DefaultValue = d.Value,
                Group = d.Group,
                DataType = d.Type,
                IsPublic = d.IsPublic,
                Description = d.Description,
                IsEditable = true
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            context.SystemConfigurations.AddRange(toInsert);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Seed: {Count} configuraciones del sistema insertadas.", toInsert.Count);
        }
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
    {
        if (await userManager.FindByNameAsync(options.AdminUserName).ConfigureAwait(false) is not null)
        {
            logger.LogInformation("Seed: el usuario {User} ya existe.", options.AdminUserName);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning(
                "Seed: AdminPassword no configurado. No se creara el usuario administrador. "
                + "Defina Seed:AdminPassword como variable de entorno.");
            return;
        }

        var admin = new ApplicationUser(options.AdminUserName, options.AdminEmail)
        {
            FirstName = "Administrador",
            LastName = "TI",
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, options.AdminPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo crear el usuario administrador: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        var assign = await userManager.AddToRoleAsync(admin, SystemRoles.SuperAdmin).ConfigureAwait(false);
        if (!assign.Succeeded)
        {
            throw new InvalidOperationException(
                $"No se pudo asignar el rol SUPER_ADMIN: {string.Join(", ", assign.Errors.Select(e => e.Description))}");
        }

        logger.LogInformation("Seed: usuario administrador {User} creado con rol {Role}.",
            options.AdminUserName, SystemRoles.SuperAdmin);
    }
}
