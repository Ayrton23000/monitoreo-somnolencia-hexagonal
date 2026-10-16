using System;

namespace Somnolencia.Application.DTOs;

public record TelemetriaOcularEntradaDto(
    Guid ConductorId,
    string EstadoOcularDetectado,
    double DuracionCierreMs,
    double ConfianzaModeloLocal,
    string FotogramaBase64
);

public record AlertaSomnolenciaRespuestaDto(
    Guid EventoId,
    Guid ConductorId,
    string NivelSeveridad,
    double DuracionCierreMs,
    string? DiagnosticoGoogleAI,
    DateTime FechaRegistro
);