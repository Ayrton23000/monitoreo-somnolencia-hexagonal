using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Somnolencia.Domain.Entities;

namespace Somnolencia.Application.Ports.Out;

public interface ISpecification<T>
{
    Expression<Func<T, bool>> Criteria { get; }
}

public interface IEventoFatigaRepositoryPort
{
    Task RegistrarConOutboxAsync(EventoFatiga evento, OutboxMessage mensajeOutbox, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EventoFatiga>> ConsultarPorEspecificacionAsync(ISpecification<EventoFatiga> spec, CancellationToken cancellationToken = default);
}