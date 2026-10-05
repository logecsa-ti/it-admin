namespace TIAdmin.Application.Administration;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Domain.Exceptions;

/// <summary>
/// Parametros del sistema (SystemConfigurations). Los valores se validan por tipo y por clave;
/// los cifrados se guardan con <see cref="ISecretProtector"/> y nunca se devuelven en claro.
/// </summary>
public interface IConfigurationService
{
    Task<IReadOnlyList<ConfigurationItemDto>> GetAllAsync(string? group, CancellationToken cancellationToken = default);

    /// <summary>Parametros publicos (IsPublic, no cifrados) para el frontend, incluso sin sesion.</summary>
    Task<IReadOnlyDictionary<string, string?>> GetPublicAsync(CancellationToken cancellationToken = default);

    Task<ConfigurationItemDto> UpdateAsync(string key, string? value, CancellationToken cancellationToken = default);

    Task<ConfigurationItemDto> ResetAsync(string key, CancellationToken cancellationToken = default);
}

public sealed partial class ConfigurationService(
    IUnitOfWork unitOfWork,
    ISecretProtector secretProtector,
    IClock clock)
    : IConfigurationService
{
    public const string Mask = "********";

    /// <summary>
    /// Claves cuyo valor efectivo viene de appsettings (AppOptions, PagedQuery): editarlas en la base
    /// no tendria efecto, asi que la API las trata como solo lectura.
    /// </summary>
    private static readonly HashSet<string> RuntimeManagedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "App.TimeZone", "App.PageSize", "App.MaxPageSize"
    };

    public async Task<IReadOnlyList<ConfigurationItemDto>> GetAllAsync(string? group, CancellationToken cancellationToken = default) =>
        (await unitOfWork.Configurations.GetAllAsync(cancellationToken))
            .Where(c => string.IsNullOrWhiteSpace(group) || string.Equals(c.Group, group, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Group)
            .ThenBy(c => c.Key)
            .Select(ToDto)
            .ToList();

    public async Task<IReadOnlyDictionary<string, string?>> GetPublicAsync(CancellationToken cancellationToken = default)
    {
        var items = (await unitOfWork.Configurations.GetAllAsync(cancellationToken))
            .Where(c => c.IsPublic && c.DataType != ConfigurationDataType.Encrypted)
            .ToDictionary(c => c.Key, c => c.Value ?? c.DefaultValue, StringComparer.OrdinalIgnoreCase);

        // La zona efectiva es la de despliegue (App:TimeZone), no la copia en base de datos.
        items["App.TimeZone"] = clock.TimeZone.Id;
        return items;
    }

    public async Task<ConfigurationItemDto> UpdateAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        var item = await FindEditableAsync(key, cancellationToken);
        item.Value = Validate(item, value);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<ConfigurationItemDto> ResetAsync(string key, CancellationToken cancellationToken = default)
    {
        var item = await FindEditableAsync(key, cancellationToken);
        item.Value = item.DataType == ConfigurationDataType.Encrypted && item.DefaultValue is { } plain
            ? secretProtector.Protect(plain)
            : item.DefaultValue;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    private async Task<SystemConfiguration> FindEditableAsync(string key, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.Configurations.GetByKeyAsync(key, cancellationToken)
            ?? throw new EntityNotFoundException($"parametro {key}");

        if (!item.IsEditable || RuntimeManagedKeys.Contains(item.Key))
        {
            throw new ConflictException("CONFIGURATION_NOT_EDITABLE",
                RuntimeManagedKeys.Contains(item.Key)
                    ? $"{item.Key} se define en la configuracion de despliegue (appsettings), no por API."
                    : $"El parametro {item.Key} no es editable.");
        }

        return item;
    }

    /// <summary>Valida y normaliza el valor segun su tipo y las reglas conocidas de cada clave.</summary>
    private string? Validate(SystemConfiguration item, string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return item.DataType == ConfigurationDataType.String ? null : throw Invalid(item, "no puede estar vacio");
        }

        var normalized = item.DataType switch
        {
            ConfigurationDataType.Int => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n.ToString(CultureInfo.InvariantCulture)
                : throw Invalid(item, "debe ser un numero entero"),
            ConfigurationDataType.Bool => bool.TryParse(value, out var b) ? (b ? "true" : "false") : throw Invalid(item, "debe ser true o false"),
            ConfigurationDataType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : throw Invalid(item, "debe tener formato AAAA-MM-DD"),
            ConfigurationDataType.Json => IsJson(value) ? value : throw Invalid(item, "debe ser JSON valido"),
            _ => value
        };

        normalized = ApplyKeyRules(item, normalized);

        return item.DataType == ConfigurationDataType.Encrypted ? secretProtector.Protect(normalized) : normalized;
    }

    /// <summary>Reglas por clave; devuelve el valor normalizado (p. ej. "60, 30" → "60,30", prefijo en mayusculas).</summary>
    private static string ApplyKeyRules(SystemConfiguration item, string value)
    {
        var key = item.Key;

        if (key.EndsWith(".NumberPrefix", StringComparison.OrdinalIgnoreCase))
        {
            value = value.ToUpperInvariant();
            if (!PrefixRegex().IsMatch(value))
            {
                throw Invalid(item, "debe tener 1 a 6 letras o numeros");
            }
        }

        if (key is "Alerts.Contract.Days" or "Alerts.License.Days")
        {
            var parts = value.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Any(p => !int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n is < 1 or > 365))
            {
                throw Invalid(item, "debe ser una lista de dias entre 1 y 365 separados por coma (ej. 90,60,30)");
            }

            value = string.Join(",", parts);
        }

        if (key == "Alerts.License.LowUtilizationPercent" && int.Parse(value, CultureInfo.InvariantCulture) is < 0 or > 100)
        {
            throw Invalid(item, "debe estar entre 0 y 100");
        }

        if ((key == "Alerts.Maintenance.Days" || key.StartsWith("Sla.Default.", StringComparison.OrdinalIgnoreCase))
            && int.Parse(value, CultureInfo.InvariantCulture) <= 0)
        {
            throw Invalid(item, "debe ser mayor que cero");
        }

        return value;
    }

    private static bool IsJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DomainValidationException Invalid(SystemConfiguration item, string reason) =>
        new("INVALID_CONFIGURATION_VALUE", $"{item.Key} {reason}.");

    private static ConfigurationItemDto ToDto(SystemConfiguration c) => new(
        c.Key,
        c.DataType == ConfigurationDataType.Encrypted ? (c.Value is null ? null : Mask) : c.Value,
        c.DataType == ConfigurationDataType.Encrypted ? (c.DefaultValue is null ? null : Mask) : c.DefaultValue,
        c.Group,
        c.DataType,
        c.Description,
        c.IsPublic,
        c.IsEditable && !RuntimeManagedKeys.Contains(c.Key),
        RuntimeManagedKeys.Contains(c.Key),
        c.UpdatedAt);

    [GeneratedRegex("^[A-Z0-9]{1,6}$")]
    private static partial Regex PrefixRegex();
}
