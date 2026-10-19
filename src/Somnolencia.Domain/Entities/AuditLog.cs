using System;

namespace Somnolencia.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public string UsuarioEjecutor { get; private set; } = string.Empty;
    public string RolEjecutor { get; private set; } = string.Empty;
    public string Accion { get; private set; } = string.Empty;
    public string Detalle { get; private set; } = string.Empty;
    public DateTime FechaEvento { get; private set; }

    private AuditLog() { }

    public AuditLog(string usuarioEjecutor, string rolEjecutor, string accion, string detalle)
    {
        Id = Guid.NewGuid();
        UsuarioEjecutor = usuarioEjecutor;
        RolEjecutor = rolEjecutor;
        Accion = accion;
        Detalle = detalle;
        FechaEvento = DateTime.Now;
    }
}