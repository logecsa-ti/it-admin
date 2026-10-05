namespace TIAdmin.Infrastructure.Services;

using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TIAdmin.Application.Common.Interfaces;
using TIAdmin.Application.Platform;

public sealed class ExportQueue : IExportQueue
{
    private readonly Channel<int> channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<int> Reader => channel.Reader;

    public void Enqueue(int exportJobId) => channel.Writer.TryWrite(exportJobId);
}

/// <summary>
/// Procesa las exportaciones asincronas. Al arrancar reencola los trabajos pendientes que quedaron
/// de una ejecucion anterior (la cola en memoria no sobrevive a un reinicio).
/// </summary>
public sealed class ExportWorker(ExportQueue queue, IServiceScopeFactory scopeFactory, ILogger<ExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            foreach (var id in await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExportJobs.GetPendingIdsAsync(stoppingToken))
            {
                queue.Enqueue(id);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "No se pudieron reencolar las exportaciones pendientes.");
        }

        await foreach (var id in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IExportService>().ProcessJobAsync(id, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Fallo el procesamiento de la exportacion {JobId}.", id);
            }
        }
    }
}
