using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Somnolencia.Application.DTOs;
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

    public async Task<IReadOnlyList<EventoCierreOcular>> ObtenerTodoElHistorialAsync(
        string? filtroConductor = null,
        string? filtroSeveridad = null,
        int? horaInicio = null,
        int? horaFin = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.EventosCierreOcular.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtroConductor))
        {
            query = query.Where(e => e.NombreConductor.ToLower().Contains(filtroConductor.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(filtroSeveridad))
        {
            query = query.Where(e => e.NivelSeveridad == filtroSeveridad);
        }

        var lista = await query
            .OrderByDescending(e => e.FechaRegistro)
            .ToListAsync(cancellationToken);

        if (horaInicio.HasValue && horaFin.HasValue)
        {
            int hIni = horaInicio.Value;
            int hFin = horaFin.Value;
            lista = lista.Where(e =>
                hIni <= hFin
                    ? (e.FechaRegistro.Hour >= hIni && e.FechaRegistro.Hour <= hFin)
                    : (e.FechaRegistro.Hour >= hIni || e.FechaRegistro.Hour <= hFin)
            ).ToList();
        }

        return lista;
    }

    public async Task<bool> EliminarEventoAsync(
        Guid eventoId,
        string usuarioAdmin,
        CancellationToken cancellationToken = default)
    {
        var evento = await _dbContext.EventosFatiga
            .FirstOrDefaultAsync(e => e.Id == eventoId, cancellationToken);

        if (evento is null)
            return false;

        evento.MarcarComoEliminado();

        var log = new AuditLog(
            usuarioAdmin,
            "Administrador",
            "ELIMINAR_REGISTRO_SOMNOLENCIA",
            $"Se elimino logicamente (Soft Delete) el evento {evento.Id} del conductor {evento.NombreConductor}."
        );

        await _dbContext.AuditLogs.AddAsync(log, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<DashboardAdminDto> ObtenerEstadisticasDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var eventos = await _dbContext.EventosCierreOcular
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var porHora = Enumerable.Range(0, 24)
            .Select(hora =>
            {
                var delBloque = eventos.Where(e => e.FechaRegistro.Hour == hora).ToList();
                return new SomnolenciaPorHoraDto(
                    hora,
                    $"{hora:D2}:00",
                    delBloque.Count,
                    delBloque.Count > 0 ? Math.Round(delBloque.Average(x => x.DuracionCierreMs), 1) : 0
                );
            })
            .ToList();

        var porTrabajador = eventos
            .GroupBy(e => new { e.ConductorId, e.NombreConductor })
            .Select(g => new PromedioPorTrabajadorDto(
                g.Key.ConductorId,
                g.Key.NombreConductor,
                g.Count(),
                Math.Round(g.Average(x => x.DuracionCierreMs), 1),
                Math.Round(g.Average(x => x.PuntajeSomnolencia), 1),
                Math.Round(g.Max(x => x.DuracionCierreMs), 1)
            ))
            .OrderByDescending(x => x.TotalIncidentes)
            .ThenByDescending(x => x.PromedioDuracionMs)
            .ToList();

        var masCritico = porTrabajador.FirstOrDefault();
        var horaPico = porHora.OrderByDescending(h => h.CantidadIncidentes).FirstOrDefault();

        return new DashboardAdminDto(
            eventos.Count,
            masCritico?.NombreConductor ?? "Sin incidentes",
            masCritico?.TotalIncidentes ?? 0,
            masCritico?.PromedioDuracionMs ?? 0,
            horaPico is not null && horaPico.CantidadIncidentes > 0
                ? $"{horaPico.EtiquetaHora} ({horaPico.CantidadIncidentes} alertas)"
                : "Sin incidentes",
            porHora,
            porTrabajador
        );
    }

    public async Task<IReadOnlyList<AuditLogDto>> ObtenerAuditLogsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.FechaEvento)
            .Take(50)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UsuarioEjecutor,
                a.RolEjecutor,
                a.Accion,
                a.Detalle,
                a.FechaEvento
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<Usuario?> ObtenerUsuarioPorUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }
    public async Task<Usuario> CrearUsuarioAsync(
        Usuario nuevoUsuario,
        string usuarioAdmin,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Usuarios.AddAsync(nuevoUsuario, cancellationToken);

        var log = new AuditLog(
            usuarioAdmin,
            "Administrador",
            "CREAR_USUARIO_BD",
            $"Se registro en PostgreSQL el usuario '{nuevoUsuario.Username}' con rol '{nuevoUsuario.Rol}'."
        );

        await _dbContext.AuditLogs.AddAsync(log, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return nuevoUsuario;
    }
    
}