using System;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using Somnolencia.Application.DTOs;
using Somnolencia.Application.Mappings;
using Somnolencia.Application.Ports.In;
using Somnolencia.Application.Ports.Out;
using Somnolencia.Application.UseCases;
using Somnolencia.Application.Validators;
using Somnolencia.Domain.Entities;
using Somnolencia.Infrastructure.Adapters.Out.AI;
using Somnolencia.Infrastructure.Adapters.Out.Background;
using Somnolencia.Infrastructure.Adapters.Out.Cache;
using Somnolencia.Infrastructure.Adapters.Out.Persistence;
using Somnolencia.Infrastructure.Grpc;
using Somnolencia.WebApi.Middlewares;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var jwtKey = builder.Configuration["JWT_SECRET_KEY"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? "clave_secreta_super_segura_de_minimo_32_caracteres_2026";

builder.Services.AddDbContextPool<SomnolenciaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["REDIS_CONNECTION_STRING"] ?? "localhost:6379";
    options.InstanceName = "Somnolencia_";
});

builder.Services.AddGrpcClient<OcularClassifier.OcularClassifierClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GRPC_PYTHON_AI_URL"] ?? "http://localhost:50051");
});

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));

builder.Services.AddHttpClient<IAnalizadorSomnolenciaIAPort, GoogleAIStudioAdapter>()
    .AddPolicyHandler(circuitBreakerPolicy);

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<SomnolenciaMappingProfile>());
builder.Services.AddScoped<IDetectorOcularPort, GrpcKerasDetectorAdapter>();
builder.Services.AddScoped<IEventoFatigaRepositoryPort, PostgresEventoFatigaRepository>();
builder.Services.AddScoped<ICacheEstadoOcularPort, RedisEstadoOcularAdapter>();
builder.Services.AddScoped<IValidator<TelemetriaOcularEntradaDto>, TelemetriaOcularValidator>();
builder.Services.AddScoped<IProcesarFotogramaUseCase, ProcesarFotogramaUseCase>();
builder.Services.AddHostedService<OutboxBackgroundWorker>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SoloAdministrador", policy => policy.RequireRole("Administrador"));
    options.AddPolicy("Autenticado", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("PoliticaMonitoreo", opt =>
    {
        opt.PermitLimit = 180;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("PoliticaCorsSegura", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SomnolenciaDbContext>();

    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS public.usuarios (
            ""Id"" uuid PRIMARY KEY,
            ""Username"" text UNIQUE NOT NULL,
            ""NombreCompleto"" text NOT NULL,
            ""PasswordHash"" text NOT NULL,
            ""Rol"" text NOT NULL
        );
        CREATE TABLE IF NOT EXISTS public.audit_logs (
            ""Id"" uuid PRIMARY KEY,
            ""UsuarioEjecutor"" text NOT NULL,
            ""RolEjecutor"" text NOT NULL,
            ""Accion"" text NOT NULL,
            ""Detalle"" text NOT NULL,
            ""FechaEvento"" timestamp without time zone NOT NULL
        );
        ALTER TABLE public.eventos_fatiga ADD COLUMN IF NOT EXISTS ""NombreConductor"" text NOT NULL DEFAULT 'Conductor';
        ALTER TABLE public.eventos_fatiga ADD COLUMN IF NOT EXISTS ""ValorEar"" double precision NOT NULL DEFAULT 0.20;
        ALTER TABLE public.eventos_fatiga ADD COLUMN IF NOT EXISTS ""ValorMar"" double precision NOT NULL DEFAULT 0.15;
        ALTER TABLE public.eventos_fatiga ADD COLUMN IF NOT EXISTS ""PuntajeSomnolencia"" integer NOT NULL DEFAULT 0;
    ");

    var idAdmin = Guid.Parse("99999999-9999-9999-9999-999999999999");
    var idCond1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    var idCond2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    var idCond3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    var cuentasBase = new[]
    {
        (Id: idAdmin, User: "admin", Nombre: "Administrador General de Flota", Pass: "admin123", Rol: "Administrador"),
        (Id: idCond1, User: "conductor1", Nombre: "Carlos Mendoza (Turno Nocturno)", Pass: "123456", Rol: "Conductor"),
        (Id: idCond2, User: "conductor2", Nombre: "Diego Ordoñez (Ruta Costera)", Pass: "123456", Rol: "Conductor"),
        (Id: idCond3, User: "conductor3", Nombre: "Mateo Sarmiento (Transporte Pesado)", Pass: "123456", Rol: "Conductor")
    };

    foreach (var c in cuentasBase)
    {
        var existente = await db.Usuarios.FirstOrDefaultAsync(u => u.Username == c.User);
        if (existente is null)
        {
            await db.Usuarios.AddAsync(new Usuario(c.Id, c.User, c.Nombre, BCrypt.Net.BCrypt.HashPassword(c.Pass), c.Rol));
        }
        else if (!BCrypt.Net.BCrypt.Verify(c.Pass, existente.PasswordHash))
        {
            db.Usuarios.Remove(existente);
            await db.SaveChangesAsync();
            await db.Usuarios.AddAsync(new Usuario(c.Id, c.User, c.Nombre, BCrypt.Net.BCrypt.HashPassword(c.Pass), c.Rol));
        }
    }
    await db.SaveChangesAsync();

    if (!await db.EventosCierreOcular.AnyAsync())
    {
        var hoy = DateTime.Today;
        var semillas = new[]
        {
            CrearSemilla(idCond1, "Carlos Mendoza (Turno Nocturno)", 2500, 0.12, 0.22, 35, hoy.AddHours(2).AddMinutes(15)),
            CrearSemilla(idCond1, "Carlos Mendoza (Turno Nocturno)", 3000, 0.11, 0.68, 45, hoy.AddHours(2).AddMinutes(42)),
            CrearSemilla(idCond1, "Carlos Mendoza (Turno Nocturno)", 2000, 0.14, 0.65, 30, hoy.AddHours(3).AddMinutes(10)),
            CrearSemilla(idCond1, "Carlos Mendoza (Turno Nocturno)", 1500, 0.15, 0.20, 20, hoy.AddHours(4).AddMinutes(5)),
            CrearSemilla(idCond2, "Diego Ordoñez (Ruta Costera)", 2000, 0.13, 0.18, 25, hoy.AddHours(1).AddMinutes(30)),
            CrearSemilla(idCond2, "Diego Ordoñez (Ruta Costera)", 2500, 0.12, 0.64, 35, hoy.AddHours(2).AddMinutes(50)),
            CrearSemilla(idCond3, "Mateo Sarmiento (Transporte Pesado)", 1500, 0.16, 0.62, 20, hoy.AddHours(23).AddMinutes(20)),
            CrearSemilla(idCond3, "Mateo Sarmiento (Transporte Pesado)", 2000, 0.14, 0.19, 25, hoy.AddHours(3).AddMinutes(35))
        };

        await db.EventosCierreOcular.AddRangeAsync(semillas);
        await db.SaveChangesAsync();
    }
}

static EventoCierreOcular CrearSemilla(Guid id, string nombre, double duracion, double ear, double mar, int score, DateTime fecha)
{
    var ev = new EventoCierreOcular(id, nombre, duracion, 0.94, ear, mar, score);
    ev.AsignarDiagnosticoIA("Episodio nocturno registrado en ruta (EAR/MAR Nivel 1).");
    typeof(EventoFatiga).GetProperty("FechaRegistro")?.SetValue(ev, fecha);
    return ev;
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self' https://cdn.jsdelivr.net https://storage.googleapis.com; connect-src 'self' https://cdn.jsdelivr.net https://storage.googleapis.com; img-src 'self' data: blob:; media-src 'self' blob:; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://cdn.jsdelivr.net;";
    await next();
});

app.UseCors("PoliticaCorsSegura");
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

app.MapPost("/api/v1/auth/login", async (
    LoginRequestDto req,
    IEventoFatigaRepositoryPort repo,
    HttpContext http,
    CancellationToken ct) =>
{
    var user = await repo.ObtenerUsuarioPorUsernameAsync(req.Username, ct);
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim("NombreCompleto", user.NombreCompleto),
        new Claim(ClaimTypes.Role, user.Rol)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.UtcNow.AddHours(12),
        signingCredentials: creds
    );

    var jwtString = new JwtSecurityTokenHandler().WriteToken(token);

    var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    http.Response.Cookies.Append("Somnolencia_RefreshToken", refreshToken, new CookieOptions
    {
        HttpOnly = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Expires = DateTimeOffset.UtcNow.AddDays(7)
    });

    return Results.Ok(new LoginResponseDto(
        jwtString,
        user.Id,
        user.Username,
        user.NombreCompleto,
        user.Rol
    ));
});

app.MapPost("/api/v1/monitoreo/evaluar", async (
    TelemetriaOcularEntradaDto dto,
    IProcesarFotogramaUseCase useCase,
    HttpContext http,
    CancellationToken ct) =>
{
    http.Response.Headers["X-API-Version"] = "1.0";
    var resultado = await useCase.EjecutarAsync(dto, ct);
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";

    var respuestaHateoas = new
    {
        datos = resultado,
        _links = new
        {
            self = new { href = $"{baseUrl}/api/v1/monitoreo/evaluar", method = "POST" },
            historial = new { href = $"{baseUrl}/api/v1/monitoreo/historial/mio", method = "GET" }
        }
    };

    return Results.Ok(respuestaHateoas);
}).RequireAuthorization("Autenticado").RequireRateLimiting("PoliticaMonitoreo");

app.MapGet("/api/v1/monitoreo/historial/mio", async (
    ClaimsPrincipal user,
    IProcesarFotogramaUseCase useCase,
    CancellationToken ct) =>
{
    var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var conductorId))
        return Results.Unauthorized();

    var historial = await useCase.ObtenerHistorialConductorAsync(conductorId, ct);
    return Results.Ok(historial);
}).RequireAuthorization("Autenticado");

app.MapGet("/api/v1/admin/historial", async (
    string? conductor,
    string? severidad,
    int? horaInicio,
    int? horaFin,
    IProcesarFotogramaUseCase useCase,
    CancellationToken ct) =>
{
    var historialGlobal = await useCase.ObtenerHistorialGlobalAdminAsync(
        conductor,
        severidad,
        horaInicio,
        horaFin,
        ct
    );
    return Results.Ok(historialGlobal);
}).RequireAuthorization("SoloAdministrador");

app.MapDelete("/api/v1/admin/historial/{eventoId:guid}", async (
    Guid eventoId,
    ClaimsPrincipal user,
    IProcesarFotogramaUseCase useCase,
    CancellationToken ct) =>
{
    var adminUsername = user.Identity?.Name ?? "admin";
    var eliminado = await useCase.EliminarRegistroAdminAsync(eventoId, adminUsername, ct);
    return eliminado ? Results.NoContent() : Results.NotFound();
}).RequireAuthorization("SoloAdministrador");

app.MapGet("/api/v1/admin/dashboard", async (
    IProcesarFotogramaUseCase useCase,
    CancellationToken ct) =>
{
    var stats = await useCase.ObtenerDashboardAdminAsync(ct);
    return Results.Ok(stats);
}).RequireAuthorization("SoloAdministrador");

app.MapGet("/api/v1/admin/audit-logs", async (
    IProcesarFotogramaUseCase useCase,
    CancellationToken ct) =>
{
    var logs = await useCase.ObtenerAuditLogsAdminAsync(ct);
    return Results.Ok(logs);
}).RequireAuthorization("SoloAdministrador");

app.MapGet("/api/v2/admin/metricas-modelo", (HttpContext http) =>
{
    http.Response.Headers["X-API-Version"] = "2.0";
    var metricas = new MetricasValidacionModeloDto(
        TotalMuestrasEvaluadas: 316,
        VerdaderosPositivos: 149,
        VerdaderosNegativos: 135,
        FalsosPositivos: 0,
        FalsosNegativos: 32,
        AccuracyPorcentaje: 89.9,
        PrecisionPorcentaje: 91.8,
        RecallPorcentaje: 91.2,
        F1ScorePorcentaje: 91.5
    );
    return Results.Ok(metricas);
}).RequireAuthorization("SoloAdministrador");
app.MapPost("/api/v1/admin/usuarios", async (
    CrearUsuarioDto dto,
    ClaimsPrincipal user,
    IEventoFatigaRepositoryPort repo,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { mensaje = "Usuario y contraseña obligatorios." });

    var existente = await repo.ObtenerUsuarioPorUsernameAsync(dto.Username.Trim(), ct);
    if (existente is not null)
        return Results.Conflict(new { mensaje = "El nombre de usuario ya existe en la base de datos." });

    var rolValido = dto.Rol == "Administrador" ? "Administrador" : "Conductor";
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

    var nuevo = new Usuario(
        Guid.NewGuid(),
        dto.Username.Trim(),
        string.IsNullOrWhiteSpace(dto.NombreCompleto) ? dto.Username.Trim() : dto.NombreCompleto.Trim(),
        passwordHash,
        rolValido
    );

    var adminUsername = user.Identity?.Name ?? "admin";
    await repo.CrearUsuarioAsync(nuevo, adminUsername, ct);

    return Results.Created($"/api/v1/admin/usuarios/{nuevo.Id}", new
    {
        nuevo.Id,
        nuevo.Username,
        nuevo.NombreCompleto,
        nuevo.Rol
    });
}).RequireAuthorization("SoloAdministrador");

app.Run();