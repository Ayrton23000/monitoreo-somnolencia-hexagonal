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

    Task<IReadOnlyList<AlertaSomnolenciaRespuestaDto>> ObtenerHistorialCriticoAsync(
        Guid conductorId,
        CancellationToken cancellationToken = default
    );
}