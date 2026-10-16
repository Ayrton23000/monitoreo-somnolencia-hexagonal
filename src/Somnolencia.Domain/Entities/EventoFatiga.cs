using System;

namespace Somnolencia.Domain.Entities;

public abstract class EventoFatiga
{
    public Guid Id { get; protected set; }
    public Guid ConductorId { get; protected set; }
    public DateTime FechaRegistro { get; protected set; }
    public string NivelSeveridad { get; protected set; } = string.Empty;
    public string? DiagnosticoContextualIA { get; protected set; }
    public bool EstaEliminado { get; protected set; }

    protected EventoFatiga() { }

    protected EventoFatiga(Guid conductorId)
    {
        Id = Guid.NewGuid();
        ConductorId = conductorId;
        FechaRegistro = DateTime.UtcNow;
        EstaEliminado = false;
    }

    public void AsignarDiagnosticoIA(string diagnostico)
    {
        if (string.IsNullOrWhiteSpace(diagnostico))
            throw new ArgumentException("El diagnostico de IA no puede estar vacio.");

        DiagnosticoContextualIA = diagnostico;
    }
}

public sealed class EventoCierreOcular : EventoFatiga
{
    public double DuracionCierreMs { get; private set; }
    public double IndiceConfianzaTensorFlow { get; private set; }

    private EventoCierreOcular() : base() { }

    public EventoCierreOcular(Guid conductorId, double duracionCierreMs, double indiceConfianzaTensorFlow)
        : base(conductorId)
    {
        DuracionCierreMs = duracionCierreMs;
        IndiceConfianzaTensorFlow = indiceConfianzaTensorFlow;
        EvaluarReglaSomnolencia();
    }

    private void EvaluarReglaSomnolencia()
    {
        if (DuracionCierreMs >= 2000)
            NivelSeveridad = "CRITICO_MICROSUEÑO";
        else if (DuracionCierreMs >= 1000)
            NivelSeveridad = "ADVERTENCIA_FATIGA";
        else
            NivelSeveridad = "PARPADEO_NORMAL";
    }

    public bool RequiereAuditoriaContextual() => DuracionCierreMs >= 1500;
}