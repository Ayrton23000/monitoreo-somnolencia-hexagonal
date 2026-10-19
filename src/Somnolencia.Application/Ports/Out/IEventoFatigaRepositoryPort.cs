using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Somnolencia.Application.DTOs;
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
    Task<IReadOnlyList<EventoCierreOcular>> ObtenerTodoElHistorialAsync(string? filtroConductor = null, string? filtroSeveridad = null, int? horaInicio = null, int? horaFin = null, CancellationToken cancellationToken = default);
    Task<bool> EliminarEventoAsync(Guid eventoId, string usuarioAdmin, CancellationToken cancellationToken = default);
    Task<DashboardAdminDto> ObtenerEstadisticasDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogDto>> ObtenerAuditLogsAsync(CancellationToken cancellationToken = default);
    Task<Usuario?> ObtenerUsuarioPorUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<Usuario> CrearUsuarioAsync(Usuario nuevoUsuario, string usuarioAdmin, CancellationToken cancellationToken = default);
}