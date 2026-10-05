namespace TIAdmin.Api.Middleware;

using System.Security.Claims;
using Serilog.Context;

/// <summary>
/// Agrega UserId y UserName a todos los logs de la peticion (despues de la autenticacion), de modo que
/// un CorrelationId lleva directamente a quien hizo que (SPECS.md seccion 41).
/// </summary>
public sealed class UserLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("UserName", context.User.Identity.Name))
        {
            await next(context);
        }
    }
}
