using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Somnolencia.Application.Ports.Out;
using Somnolencia.Domain.Entities;

namespace Somnolencia.Infrastructure.Adapters.Out.Persistence;

public sealed class PostgresEventoFatigaRepository : IEventoFatigaRepositoryPort
{
    private readonly SomnolenciaDbContext _dbContext;

    public PostgresEventoFatigaRepository(SomnolenciaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RegistrarConOutboxAsync(
        EventoFatiga evento,
        OutboxMessage mensajeOutbox,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted,
            cancellationToken
        );

        try
        {
            await _dbContext.EventosFatiga.AddAsync(evento, cancellationToken);
            await _dbContext.OutboxMessages.AddAsync(mensajeOutbox, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<EventoFatiga>> ConsultarPorEspecificacionAsync(
        ISpecification<EventoFatiga> spec,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.EventosFatiga
            .AsNoTracking()
            .Where(spec.Criteria)
            .OrderByDescending(e => e.FechaRegistro)
            .ToListAsync(cancellationToken);
    }
}