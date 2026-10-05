namespace TIAdmin.Domain.Services;

/// <summary>Jornada laboral sobre la que corre un SLA.</summary>
public sealed record WorkSchedule(bool BusinessHoursOnly, TimeOnly Start, TimeOnly End, IReadOnlySet<DayOfWeek> WorkDays)
{
    public static readonly WorkSchedule AlwaysOn = new(false, TimeOnly.MinValue, TimeOnly.MaxValue, new HashSet<DayOfWeek>());

    /// <summary>"1,2,3,4,5" (1 = lunes ... 7 = domingo) → dias laborables.</summary>
    public static IReadOnlySet<DayOfWeek> ParseDays(string? days)
    {
        var result = new HashSet<DayOfWeek>();
        foreach (var part in (days ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var n) && n is >= 1 and <= 7)
            {
                result.Add(n == 7 ? DayOfWeek.Sunday : (DayOfWeek)n);
            }
        }

        return result;
    }
}

/// <summary>
/// Calculo de vencimientos en horario laboral (SPECS.md seccion 22). Opera en la zona horaria
/// de la organizacion y devuelve UTC.
/// </summary>
public static class BusinessHours
{
    public static DateTime AddWorkingMinutes(DateTime startUtc, int minutes, WorkSchedule schedule, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);

        // Sin horario laboral valido (o sin dias laborables) el reloj corre 24x7.
        if (minutes == 0 || !schedule.BusinessHoursOnly || schedule.WorkDays.Count == 0 || schedule.End <= schedule.Start)
        {
            return DateTime.SpecifyKind(startUtc, DateTimeKind.Utc).AddMinutes(minutes);
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), timeZone);
        double remaining = minutes;

        while (true)
        {
            if (!schedule.WorkDays.Contains(local.DayOfWeek))
            {
                local = local.Date.AddDays(1).Add(schedule.Start.ToTimeSpan());
                continue;
            }

            var dayStart = local.Date.Add(schedule.Start.ToTimeSpan());
            var dayEnd = local.Date.Add(schedule.End.ToTimeSpan());

            if (local < dayStart)
            {
                local = dayStart;
            }

            if (local >= dayEnd)
            {
                local = local.Date.AddDays(1).Add(schedule.Start.ToTimeSpan());
                continue;
            }

            var available = (dayEnd - local).TotalMinutes;
            if (remaining <= available)
            {
                local = local.AddMinutes(remaining);
                break;
            }

            remaining -= available;
            local = local.Date.AddDays(1).Add(schedule.Start.ToTimeSpan());
        }

        return ToUtc(local, timeZone);
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // En zonas con horario de verano, una hora inexistente se adelanta al primer instante valido.
        while (timeZone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddMinutes(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }
}
