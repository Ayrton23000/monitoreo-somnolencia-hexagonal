using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Somnolencia.Application.Ports.Out;

namespace Somnolencia.Infrastructure.Adapters.Out.Cache;

public sealed class RedisEstadoOcularAdapter : ICacheEstadoOcularPort
{
    private readonly IDistributedCache _cache;

    public RedisEstadoOcularAdapter(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<double> ActualizarDuracionOjosCerradosAsync(
        Guid conductorId,
        bool ojosCerrados,
        double intervaloMs,
        CancellationToken cancellationToken = default)
    {
        var key = $"conductor:ojos_cerrados_ms:{conductorId}";

        if (!ojosCerrados)
        {
            await _cache.RemoveAsync(key, cancellationToken);
            return 0;
        }

        var valorActualStr = await _cache.GetStringAsync(key, cancellationToken);
        double acumulado = 0;

        if (!string.IsNullOrEmpty(valorActualStr))
        {
            double.TryParse(valorActualStr, NumberStyles.Any, CultureInfo.InvariantCulture, out acumulado);
        }

        acumulado += intervaloMs;

        var opciones = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        await _cache.SetStringAsync(
            key,
            acumulado.ToString(CultureInfo.InvariantCulture),
            opciones,
            cancellationToken
        );

        return acumulado;
    }
}