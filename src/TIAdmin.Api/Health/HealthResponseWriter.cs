namespace TIAdmin.Api.Health;

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Respuesta JSON de /health y /health/ready: estado global y por componente (SPECS.md seccion 42).
/// No incluye excepciones ni cadenas de conexion: los endpoints son anonimos.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                description = entry.Value.Description
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, Options));
    }
}
