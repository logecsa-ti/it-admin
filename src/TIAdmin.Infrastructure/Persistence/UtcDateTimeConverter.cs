namespace TIAdmin.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

/// <summary>
/// Persiste DateTime en UTC y lo devuelve con <see cref="DateTimeKind.Utc"/>.
/// Al escribir: un valor Local se convierte a UTC; uno Unspecified se asume ya en UTC.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => ToUtc(value),
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
