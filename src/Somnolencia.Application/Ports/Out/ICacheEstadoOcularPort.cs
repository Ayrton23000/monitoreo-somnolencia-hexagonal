using System;
using System.Threading;
using System.Threading.Tasks;

namespace Somnolencia.Application.Ports.Out;

public interface ICacheEstadoOcularPort
{
    Task<double> ActualizarDuracionOjosCerradosAsync(
        Guid conductorId,
        bool ojosCerrados,
        double intervaloMs,
        CancellationToken cancellationToken = default
    );
}