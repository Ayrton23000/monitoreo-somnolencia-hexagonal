using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Somnolencia.Application.DTOs;
using Somnolencia.Application.Ports.In;
using Somnolencia.Application.Ports.Out;
using Somnolencia.Domain.Entities;

namespace Somnolencia.Application.UseCases;

public sealed class EventosPorConductorSpecification : ISpecification<EventoFatiga>
{
    public Expression<Func<EventoFatiga, bool>> Criteria { get; }

    public EventosPorConductorSpecification(Guid conductorId)
    {
        Criteria = e => e.ConductorId == conductorId;
    }
}

public sealed class ProcesarFotogramaUseCase : IProcesarFotogramaUseCase
{
    private readonly IDetectorOcularPort _detectorOcular;
    private readonly IAnalizadorSomnolenciaIAPort _analizadorIA;
    private readonly IEventoFatigaRepositoryPort _repositorio;
    private readonly ICacheEstadoOcularPort _cache;
    private readonly IValidator<TelemetriaOcularEntradaDto> _validador;

    public ProcesarFotogramaUseCase(
        IDetectorOcularPort detectorOcular,
        IAnalizadorSomnolenciaIAPort analizadorIA,
        IEventoFatigaRepositoryPort repositorio,
        ICacheEstadoOcularPort cache,
        IValidator<TelemetriaOcularEntradaDto> validador)
    {
        _detectorOcular = detectorOcular;
        _analizadorIA = analizadorIA;
        _repositorio = repositorio;
        _cache = cache;
        _validador = validador;
    }

    public async Task<AlertaSomnolenciaRespuestaDto> EjecutarAsync(
        TelemetriaOcularEntradaDto entrada,
        CancellationToken cancellationToken = default)
    {
        await _validador.ValidateAndThrowAsync(entrada, cancellationToken);

        var base64Limpio = entrada.FotogramaBase64.Contains(',')
            ? entrada.FotogramaBase64.Split(',')[1]
            : entrada.FotogramaBase64;

        var imagenBytes = Convert.FromBase64String(base64Limpio);

        var inferencia = await _detectorOcular.ClasificarFotogramaAsync(imagenBytes, cancellationToken);

        var duracionAcumuladaMs = await _cache.ActualizarDuracionOjosCerradosAsync(
            entrada.ConductorId,
            inferencia.OjosCerrados,
            entrada.DuracionCierreMs,
            cancellationToken
        );

        var evento = new EventoCierreOcular(
            entrada.ConductorId,
            duracionAcumuladaMs,
            inferencia.Confianza
        );

        if (evento.RequiereAuditoriaContextual())
        {
            try
            {
                var diagnosticoIA = await _analizadorIA.AnalizarFotogramaSomnolenciaAsync(
                    base64Limpio,
                    duracionAcumuladaMs,
                    cancellationToken
                );
                evento.AsignarDiagnosticoIA(diagnosticoIA);
            }
            catch
            {
                evento.AsignarDiagnosticoIA("Alerta local confirmada por TensorFlow. Diagnostico externo diferido por Circuit Breaker.");
            }

            var outbox = new OutboxMessage(
                "ALERTA_SOMNOLENCIA_DETECTADA",
                JsonSerializer.Serialize(new { evento.Id, evento.ConductorId, evento.NivelSeveridad, duracionAcumuladaMs })
            );

            await _repositorio.RegistrarConOutboxAsync(evento, outbox, cancellationToken);
        }

        return new AlertaSomnolenciaRespuestaDto(
            evento.Id,
            evento.ConductorId,
            $"{inferencia.EstadoOcular} - {evento.NivelSeveridad}",
            duracionAcumuladaMs,
            evento.DiagnosticoContextualIA,
            evento.FechaRegistro
        );
    }

    public async Task<IReadOnlyList<AlertaSomnolenciaRespuestaDto>> ObtenerHistorialCriticoAsync(
        Guid conductorId,
        CancellationToken cancellationToken = default)
    {
        var spec = new EventosPorConductorSpecification(conductorId);
        var eventos = await _repositorio.ConsultarPorEspecificacionAsync(spec, cancellationToken);

        return eventos.OfType<EventoCierreOcular>()
            .Select(e => new AlertaSomnolenciaRespuestaDto(
                e.Id,
                e.ConductorId,
                e.NivelSeveridad,
                e.DuracionCierreMs,
                e.DiagnosticoContextualIA,
                e.FechaRegistro
            ))
            .ToList();
    }
}