using System;

namespace Somnolencia.Domain.Entities;

public abstract class EventoFatiga
{
    public Guid Id { get; protected set; }
    public Guid ConductorId { get; protected set; }
    public string NombreConductor { get; protected set; } = string.Empty;
    public DateTime FechaRegistro { get; protected set; }
    public string NivelSeveridad { get; protected set; } = string.Empty;
    public string? DiagnosticoContextualIA { get; protected set; }
    public bool EstaEliminado { get; protected set; }

    protected EventoFatiga() { }

    protected EventoFatiga(Guid conductorId, string nombreConductor)
    {
        Id = Guid.NewGuid();
        ConductorId = conductorId;
        NombreConductor = string.IsNullOrWhiteSpace(nombreConductor) ? "Conductor" : nombreConductor;
        FechaRegistro = DateTime.Now;
        EstaEliminado = false;
    }

    public void AsignarDiagnosticoIA(string diagnostico)
    {
        if (string.IsNullOrWhiteSpace(diagnostico))
            throw new ArgumentException("El diagnostico de IA no puede estar vacio.");

        DiagnosticoContextualIA = diagnostico;
    }

    public void MarcarComoEliminado()
    {
        EstaEliminado = true;
    }
}

public sealed class EventoCierreOcular : EventoFatiga
{
    public double DuracionCierreMs { get; private set; }
    public double IndiceConfianzaTensorFlow { get; private set; }
    public double ValorEar { get; private set; }
    public double ValorMar { get; private set; }
    public int PuntajeSomnolencia { get; private set; }

    private EventoCierreOcular() : base() { }

    public EventoCierreOcular(
        Guid conductorId,
        string nombreConductor,
        double duracionCierreMs,
        double indiceConfianzaTensorFlow,
        double valorEar,
        double valorMar,
        int puntajeSomnolencia)
        : base(conductorId, nombreConductor)
    {
        DuracionCierreMs = duracionCierreMs;
        IndiceConfianzaTensorFlow = indiceConfianzaTensorFlow;
        ValorEar = valorEar;
        ValorMar = valorMar;
        PuntajeSomnolencia = puntajeSomnolencia;
        EvaluarReglaSomnolencia();
    }

    private void EvaluarReglaSomnolencia()
    {
        if (DuracionCierreMs >= 2000)
            NivelSeveridad = "CRITICO_MICROSUEÑO";
        else if (DuracionCierreMs >= 1500)
            NivelSeveridad = "NIVEL_1_SOMNOLENCIA";
        else if (ValorMar >= 0.60 && PuntajeSomnolencia >= 15)
            NivelSeveridad = "ALERTA_BOSTEZO_PROLONGADO";
        else
            NivelSeveridad = "NIVEL_0_NORMAL";
    }

    public bool RequiereAuditoriaContextual() =>
        DuracionCierreMs >= 1500 || NivelSeveridad == "ALERTA_BOSTEZO_PROLONGADO";
}