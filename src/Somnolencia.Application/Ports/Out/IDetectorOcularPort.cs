using System.Threading;
using System.Threading.Tasks;

namespace Somnolencia.Application.Ports.Out;

public record ResultadoInferenciaOcular(
    string EstadoOcular,
    double Confianza,
    bool OjosCerrados
);

public interface IDetectorOcularPort
{
    Task<ResultadoInferenciaOcular> ClasificarFotogramaAsync(
        byte[] imagenBytes,
        CancellationToken cancellationToken = default
    );
}