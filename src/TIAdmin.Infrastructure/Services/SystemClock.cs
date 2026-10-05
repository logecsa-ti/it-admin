namespace TIAdmin.Infrastructure.Services;

using Microsoft.Extensions.Options;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Common.Models;

/// <summary>
/// <see cref="UtcNow"/> para todo lo que se persiste. <see cref="Today"/> es la fecha de negocio
/// en la zona horaria de la organizacion (App:TimeZone): los vencimientos de contratos y licencias
/// se evaluan contra ella, no contra la fecha UTC (que en Managua cambia a las 18:00).
/// </summary>
public sealed class SystemClock(IOptions<AppOptions> options) : IClock
{
    public TimeZoneInfo TimeZone { get; } = ResolveTimeZone(options.Value.TimeZone);

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Today => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone).Date;

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
        {
            return zone;
        }

        return TimeZoneInfo.Utc;
    }
}
