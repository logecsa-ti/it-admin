namespace TIAdmin.Infrastructure.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TIAdmin.Application.Common.Interfaces;

/// <summary>
/// Implementa ICurrentUserService leyendo el ClaimsPrincipal de la peticion actual.
/// Vive en Infrastructure porque depende de ASP.NET Core.
/// </summary>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => User?.FindFirstValue(ClaimTypes.Name) ?? User?.Identity?.Name;

    public string? IpAddress
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll(TiClaimTypes.Permission).Select(c => c.Value).ToArray() ?? [];

    public string? UserAgent() =>
        httpContextAccessor.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();

    public string? CorrelationId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
            {
                return null;
            }

            // Items es la fuente de verdad por peticion (CorrelationIdMiddleware).
            // El claim se usa como respaldo cuando el token se emite fuera del pipeline HTTP.
            return context.Items.TryGetValue("CorrelationId", out var value)
                ? value?.ToString()
                : User?.FindFirstValue(TiClaimTypes.CorrelationId);
        }
    }
}

/// <summary>
/// Tipos de claims proprios de TI Admin.
/// </summary>
public static class TiClaimTypes
{
    public const string Permission = "permission";

    public const string CorrelationId = "correlation_id";
}
