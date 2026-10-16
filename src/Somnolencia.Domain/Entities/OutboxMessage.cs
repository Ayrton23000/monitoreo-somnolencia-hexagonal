using System;

namespace Somnolencia.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string TipoEvento { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaProcesamiento { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string tipoEvento, string payloadJson)
    {
        Id = Guid.NewGuid();
        TipoEvento = tipoEvento;
        PayloadJson = payloadJson;
        FechaCreacion = DateTime.UtcNow;
    }

    public void MarcarComoProcesado()
    {
        FechaProcesamiento = DateTime.UtcNow;
    }
}