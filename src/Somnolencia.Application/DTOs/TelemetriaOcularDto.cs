using System;
using System.Collections.Generic;

namespace Somnolencia.Application.DTOs;

public record TelemetriaOcularEntradaDto(
    Guid ConductorId,
    string NombreConductor,
    string EstadoOcularDetectado,
    double DuracionCierreMs,
    double ConfianzaModeloLocal,
    double ValorEar,
    double ValorMar,
    int PuntajeSomnolencia,
    string FotogramaBase64
);

public record AlertaSomnolenciaRespuestaDto(
    Guid EventoId,
    Guid ConductorId,
    string NombreConductor,
    string NivelSeveridad,
    double DuracionCierreMs,
    double ValorEar,
    double ValorMar,
    int PuntajeSomnolencia,
    string? DiagnosticoGoogleAI,
    DateTime FechaRegistro
);

public record LoginRequestDto(string Username, string Password);

public record LoginResponseDto(
    string Token,
    Guid UsuarioId,
    string Username,
    string NombreCompleto,
    string Rol
);

public record SomnolenciaPorHoraDto(
    int Hora,
    string EtiquetaHora,
    int CantidadIncidentes,
    double DuracionPromedioMs
);

public record PromedioPorTrabajadorDto(
    Guid ConductorId,
    string NombreConductor,
    int TotalIncidentes,
    double PromedioDuracionMs,
    double PromedioPuntaje,
    double MaximoCierreMs
);

public record DashboardAdminDto(
    int TotalMicrosuenosRegistrados,
    string TrabajadorConMasSomnolencia,
    int IncidentesTrabajadorCritico,
    double PromedioMsTrabajadorCritico,
    string FranjaHorariaMasCritica,
    IReadOnlyList<SomnolenciaPorHoraDto> SomnolenciaPorHora,
    IReadOnlyList<PromedioPorTrabajadorDto> PromedioPorTrabajador
);

public record MetricasValidacionModeloDto(
    int TotalMuestrasEvaluadas,
    int VerdaderosPositivos,
    int VerdaderosNegativos,
    int FalsosPositivos,
    int FalsosNegativos,
    double AccuracyPorcentaje,
    double PrecisionPorcentaje,
    double RecallPorcentaje,
    double F1ScorePorcentaje
);

public record AuditLogDto(
    Guid Id,
    string UsuarioEjecutor,
    string RolEjecutor,
    string Accion,
    string Detalle,
    DateTime FechaEvento
);
public record CrearUsuarioDto(
    string Username,
    string NombreCompleto,
    string Password,
    string Rol
);