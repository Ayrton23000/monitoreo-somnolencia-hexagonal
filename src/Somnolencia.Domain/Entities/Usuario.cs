using System;

namespace Somnolencia.Domain.Entities;

public sealed class Usuario
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string NombreCompleto { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Rol { get; private set; } = string.Empty;

    private Usuario() { }

    public Usuario(Guid id, string username, string nombreCompleto, string passwordHash, string rol)
    {
        Id = id;
        Username = username;
        NombreCompleto = nombreCompleto;
        PasswordHash = passwordHash;
        Rol = rol;
    }
}