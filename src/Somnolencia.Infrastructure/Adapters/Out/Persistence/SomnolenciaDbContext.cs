using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Somnolencia.Domain.Entities;

namespace Somnolencia.Infrastructure.Adapters.Out.Persistence;

public sealed class SomnolenciaDbContext : DbContext
{
    public DbSet<EventoFatiga> EventosFatiga => Set<EventoFatiga>();
    public DbSet<EventoCierreOcular> EventosCierreOcular => Set<EventoCierreOcular>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public SomnolenciaDbContext(DbContextOptions<SomnolenciaDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventoFatiga>(entity =>
        {
            entity.ToTable("eventos_fatiga");
            entity.HasKey(e => e.Id);

            entity.HasDiscriminator<string>("tipo_evento_fatiga")
                .HasValue<EventoCierreOcular>("CIERRE_OCULAR");

            entity.Property<DateTime>("FechaActualizacion");

            entity.Property<uint>("VersionFila")
                .IsRowVersion();

            entity.HasQueryFilter(e => !e.EstaEliminado);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(o => o.Id);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);
        });

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<EventoFatiga>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Property("FechaActualizacion").CurrentValue = DateTime.Now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}