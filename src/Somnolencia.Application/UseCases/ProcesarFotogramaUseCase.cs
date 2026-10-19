using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
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
    private readonly IMapper _mapper;

    public ProcesarFotogramaUseCase(
        IDetectorOcularPort detectorOcular,
        IAnalizadorSomnolenciaIAPort analizadorIA,
        IEventoFatigaRepositoryPort repositorio,
        ICacheEstadoOcularPort cache,
        IValidator<TelemetriaOcularEntradaDto> validador,
        IMapper mapper)
    {
        _detectorOcular = detectorOcular;
        _analizadorIA = analizadorIA;
        _repositorio = repositorio;
        _cache = cache;
        _validador = validador;
        _mapper = mapper;
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

        bool ojosCerradosConfirmados;
        if (entrada.EstadoOcularDetectado == "ABIERTO_PARPADO")
        {
            ojosCerradosConfirmados = false;
        }
        else if (entrada.EstadoOcularDetectado == "CERRADO_PARPADO")
        {
            ojosCerradosConfirmados = true;
        }
        else
        {
            ojosCerradosConfirmados = inferencia.OjosCerrados;
        }

        string etiquetaEstado = ojosCerradosConfirmados ? "CERRADO" : "ABIERTO";

        var duracionAcumuladaMs = await _cache.ActualizarDuracionOjosCerradosAsync(
            entrada.ConductorId,
            ojosCerradosConfirmados,
            entrada.DuracionCierreMs,
            cancellationToken
        );

        var evento = new EventoCierreOcular(
            entrada.ConductorId,
            entrada.NombreConductor,
            duracionAcumuladaMs,
            inferencia.Confianza,
            entrada.ValorEar,
            entrada.ValorMar,
            entrada.PuntajeSomnolencia
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
                evento.AsignarDiagnosticoIA($"Alerta Nivel 1 confirmada (EAR: {entrada.ValorEar:F2}, MAR: {entrada.ValorMar:F2}, Score: {entrada.PuntajeSomnolencia}).");
            }

            var outbox = new OutboxMessage(
                "ALERTA_SOMNOLENCIA_DETECTADA",
                JsonSerializer.Serialize(new
                {
                    evento.Id,
                    evento.ConductorId,
                    evento.NombreConductor,
                    evento.NivelSeveridad,
                    duracionAcumuladaMs,
                    entrada.ValorEar,
                    entrada.ValorMar,
                    entrada.PuntajeSomnolencia
                })
            );

            await _repositorio.RegistrarConOutboxAsync(evento, outbox, cancellationToken);
        }

        return new AlertaSomnolenciaRespuestaDto(
            evento.Id,
            evento.ConductorId,
            evento.NombreConductor,
            $"{etiquetaEstado} - {evento.NivelSeveridad}",
            duracionAcumuladaMs,
            evento.ValorEar,
            evento.ValorMar,
            evento.PuntajeSomnolencia,
            evento.DiagnosticoContextualIA,
            evento.FechaRegistro
        );
    }

    public async Task<IReadOnlyList<AlertaSomnolenciaRespuestaDto>> ObtenerHistorialConductorAsync(
        Guid conductorId,
        CancellationToken cancellationToken = default)
    {
        var spec = new EventosPorConductorSpecification(conductorId);
        var eventos = await _repositorio.ConsultarPorEspecificacionAsync(spec, cancellationToken);

        return eventos.OfType<EventoCierreOcular>()
            .Select(e => _mapper.Map<AlertaSomnolenciaRespuestaDto>(e))
            .ToList();
    }

    public async Task<IReadOnlyList<AlertaSomnolenciaRespuestaDto>> ObtenerHistorialGlobalAdminAsync(
        string? filtroConductor = null,
        string? filtroSeveridad = null,
        int? horaInicio = null,
        int? horaFin = null,
        CancellationToken cancellationToken = default)
    {
        var eventos = await _repositorio.ObtenerTodoElHistorialAsync(
            filtroConductor,
            filtroSeveridad,
            horaInicio,
            horaFin,
            cancellationToken
        );

        return eventos.Select(e => _mapper.Map<AlertaSomnolenciaRespuestaDto>(e)).ToList();
    }

    public async Task<bool> EliminarRegistroAdminAsync(
        Guid eventoId,
        string usuarioAdmin,
        CancellationToken cancellationToken = default)
    {
        return await _repositorio.EliminarEventoAsync(eventoId, usuarioAdmin, cancellationToken);
    }

    public async Task<DashboardAdminDto> ObtenerDashboardAdminAsync(
        CancellationToken cancellationToken = default)
    {
        return await _repositorio.ObtenerEstadisticasDashboardAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> ObtenerAuditLogsAdminAsync(
        CancellationToken cancellationToken = default)
    {
        return await _repositorio.ObtenerAuditLogsAsync(cancellationToken);
    }
}