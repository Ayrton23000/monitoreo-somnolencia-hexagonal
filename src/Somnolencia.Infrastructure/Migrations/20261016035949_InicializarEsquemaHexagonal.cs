using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Somnolencia.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicializarEsquemaHexagonal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos_fatiga",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConductorId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NivelSeveridad = table.Column<string>(type: "text", nullable: false),
                    DiagnosticoContextualIA = table.Column<string>(type: "text", nullable: true),
                    EstaEliminado = table.Column<bool>(type: "boolean", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    tipo_evento_fatiga = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    DuracionCierreMs = table.Column<double>(type: "double precision", nullable: true),
                    IndiceConfianzaTensorFlow = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventos_fatiga", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEvento = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FechaProcesamiento = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_fatiga");

            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
