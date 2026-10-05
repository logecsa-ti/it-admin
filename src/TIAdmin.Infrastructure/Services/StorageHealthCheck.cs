namespace TIAdmin.Infrastructure.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using TIAdmin.Application.Common.Interfaces;

/// <summary>/health/ready: el almacenamiento de archivos debe admitir escritura (SPECS.md seccion 42).</summary>
public sealed class StorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await storage.CanWriteAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Almacenamiento de archivos con escritura.")
            : HealthCheckResult.Unhealthy("No se puede escribir en el almacenamiento de archivos (Storage:RootPath).");
}
