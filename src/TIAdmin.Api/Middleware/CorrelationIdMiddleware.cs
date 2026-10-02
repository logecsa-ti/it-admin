namespace TIAdmin.Api.Middleware;

using System.Diagnostics;

/// <summary>
/// Asigna un CorrelationId a cada peticion y lo propaga en la respuesta.
/// Permite correlacionar logs, auditoria y el traceId que ve el usuario final
/// (SPECS.md secciones 18, 41 y 43).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 64)
        {
            correlationId = context.TraceIdentifier;
        }

        context.Items[LogItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (Serilog.Context.LogContext.PushProperty(LogItemKey, correlationId))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdExtensions
{
    public static string? GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.LogItemKey, out var value)
            ? value?.ToString()
            : null;
}
