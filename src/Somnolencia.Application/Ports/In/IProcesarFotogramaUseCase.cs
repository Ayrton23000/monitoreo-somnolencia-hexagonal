using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Somnolencia.Application.DTOs;

namespace Somnolencia.Application.Ports.In;

public interface IProcesarFotogramaUseCase
{
    Task<AlertaSomnolenciaRespuestaDto> EjecutarAsync(
        TelemetriaOcularEntradaDto entrada,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<AlertaSomnolenciaRespuestaDto>> ObtenerHistorialConductorAsync(
        Guid conductorId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<AlertaSomnolenciaRespuestaDto>> ObtenerHistorialGlobalAdminAsync(
        string? filtroConductor = null,
        string? filtroSeveridad = null,
        int? horaInicio = null,
        int? horaFin = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> EliminarRegistroAdminAsync(
        Guid eventoId,
        string usuarioAdmin,
        CancellationToken cancellationToken = default
    );

    Task<DashboardAdminDto> ObtenerDashboardAdminAsync(
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<AuditLogDto>> ObtenerAuditLogsAdminAsync(
        CancellationToken cancellationToken = default
    );
}