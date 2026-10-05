namespace TIAdmin.Infrastructure.Services;

using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Domain.Entities;
using TIAdmin.Domain.Enums;
using TIAdmin.Infrastructure.Persistence;

/// <summary>Lee SystemConfigurations; usa el valor por defecto del seed si falta o es invalido.</summary>
/// <remarks>Los parametros de tipo Encrypted se descifran al leerse (se guardan con ISecretProtector).</remarks>
public sealed class SystemSettings(TIAdminDbContext context, ISecretProtector secretProtector) : ISystemSettings
{
    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken cancellationToken = default) =>
        await GetValueAsync(key, cancellationToken) is { Length: > 0 } value ? value.Trim() : fallback;

    public async Task<IReadOnlyList<int>> GetIntListAsync(string key, IReadOnlyList<int> fallback, CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var parsed = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : (int?)null)
            .Where(n => n is > 0)
            .Select(n => n!.Value)
            .Distinct()
            .OrderDescending()
            .ToList();

        return parsed.Count > 0 ? parsed : fallback;
    }

    private async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken)
    {
        var row = await context.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.Key == key)
            .Select(c => new { c.Value, c.DefaultValue, c.DataType })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        // El valor por defecto de un parametro cifrado se siembra en claro; el valor editado, cifrado.
        return row.DataType == ConfigurationDataType.Encrypted && row.Value is { } protectedValue
            ? secretProtector.Unprotect(protectedValue)
            : row.Value ?? row.DefaultValue;
    }
}

/// <summary>
/// Cifrado con ASP.NET Core Data Protection (Q-09). El anillo de claves debe persistirse y protegerse
/// fuera de la base de datos en produccion (DataProtection:KeysPath); si se pierde, las claves de
/// licencia cifradas ya no pueden descifrarse.
/// </summary>
public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("TIAdmin.Secrets.v1");

    public string Protect(string plaintext) => protector.Protect(plaintext);

    public string Unprotect(string protectedValue) => protector.Unprotect(protectedValue);
}

public sealed class AuditLogger(
    TIAdminDbContext context,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    IClock clock)
    : IAuditLogger
{
    public async Task LogAsync(
        AuditAction action,
        string module,
        string entityName,
        string entityId,
        string? details,
        CancellationToken cancellationToken = default)
    {
        context.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            UserName = Truncate(currentUser.UserName, 256),
            Action = action,
            Module = module,
            EntityName = entityName,
            EntityId = entityId,
            NewValues = details is null ? null : System.Text.Json.JsonSerializer.Serialize(new { Details = details }),
            IpAddress = Truncate(currentUser.IpAddress, 45),
            UserAgent = Truncate(currentUser.UserAgent(), 500),
            Timestamp = clock.UtcNow,
            CorrelationId = Truncate(auditContext.CorrelationId, 64),
            IsError = false
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
