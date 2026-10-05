namespace TIAdmin.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TIAdmin.Application.Platform;

/// <summary>
/// Scheduler (Q-04: BackgroundService, sin Quartz). Ejecuta el barrido de alertas cada
/// Jobs:AlertScanIntervalMinutes (60 por defecto). Jobs:Enabled=false lo desactiva (pruebas).
/// Con varias instancias, la deduplicacion por indice unico evita notificaciones repetidas.
/// </summary>
public sealed class AlertScanWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AlertScanWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Jobs:Enabled", true))
        {
            logger.LogInformation("Scheduler de alertas deshabilitado (Jobs:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Jobs:AlertScanIntervalMinutes", 60)));

        // Espera inicial: no competir con el arranque (migraciones, seed).
        await Task.Delay(TimeSpan.FromSeconds(configuration.GetValue("Jobs:StartupDelaySeconds", 30)), stoppingToken);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IAlertNotificationJob>().RunAsync(stoppingToken);
                logger.LogInformation("Barrido de alertas: {Total} notificaciones nuevas ({@Result}).", result.Total, result);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Fallo el barrido de alertas; se reintentara en {Interval}.", interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
