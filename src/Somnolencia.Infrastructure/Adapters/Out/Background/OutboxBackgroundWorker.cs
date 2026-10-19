using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Somnolencia.Infrastructure.Adapters.Out.Persistence;

namespace Somnolencia.Infrastructure.Adapters.Out.Background;

public sealed class OutboxBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxBackgroundWorker> _logger;

    public OutboxBackgroundWorker(IServiceProvider serviceProvider, ILogger<OutboxBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SomnolenciaDbContext>();

                var pendientes = await dbContext.OutboxMessages
                    .Where(m => m.FechaProcesamiento == null)
                    .OrderBy(m => m.FechaCreacion)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var mensaje in pendientes)
                {
                    mensaje.MarcarComoProcesado();
                    _logger.LogInformation("Outbox procesado en segundo plano: {Id} - {Tipo}", mensaje.Id, mensaje.TipoEvento);
                }

                if (pendientes.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Ciclo Outbox en espera: {Mensaje}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}