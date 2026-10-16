using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Somnolencia.Application.Ports.Out;
using Somnolencia.Infrastructure.Grpc;

namespace Somnolencia.Infrastructure.Adapters.Out.AI;

public sealed class GrpcKerasDetectorAdapter : IDetectorOcularPort
{
    private readonly OcularClassifier.OcularClassifierClient _grpcClient;

    public GrpcKerasDetectorAdapter(OcularClassifier.OcularClassifierClient grpcClient)
    {
        _grpcClient = grpcClient;
    }

    public async Task<ResultadoInferenciaOcular> ClasificarFotogramaAsync(
        byte[] imagenBytes,
        CancellationToken cancellationToken = default)
    {
        var request = new FotogramaRequest
        {
            ImagenBytes = ByteString.CopyFrom(imagenBytes)
        };

        var reply = await _grpcClient.ClasificarEstadoOcularAsync(
            request,
            cancellationToken: cancellationToken
        );

        return new ResultadoInferenciaOcular(
            reply.EstadoOcular,
            reply.Confianza,
            reply.OjosCerrados
        );
    }
}