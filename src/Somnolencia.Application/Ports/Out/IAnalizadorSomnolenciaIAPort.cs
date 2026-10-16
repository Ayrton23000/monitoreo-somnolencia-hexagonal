using System.Threading;
using System.Threading.Tasks;

namespace Somnolencia.Application.Ports.Out;

public interface IAnalizadorSomnolenciaIAPort
{
    Task<string> AnalizarFotogramaSomnolenciaAsync(
        string fotogramaBase64,
        double duracionCierreMs,
        CancellationToken cancellationToken = default
    );
}