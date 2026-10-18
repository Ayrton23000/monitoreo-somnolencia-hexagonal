using System;
using System.Threading;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using Polly.Extensions.Http;
using Somnolencia.Application.DTOs;
using Somnolencia.Application.Ports.In;
using Somnolencia.Application.Ports.Out;
using Somnolencia.Application.UseCases;
using Somnolencia.Application.Validators;
using Somnolencia.Infrastructure.Adapters.Out.AI;
using Somnolencia.Infrastructure.Adapters.Out.Cache;
using Somnolencia.Infrastructure.Adapters.Out.Persistence;
using Somnolencia.Infrastructure.Grpc;
using Somnolencia.WebApi.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

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

builder.Services.AddScoped<IDetectorOcularPort, GrpcKerasDetectorAdapter>();
builder.Services.AddScoped<IEventoFatigaRepositoryPort, PostgresEventoFatigaRepository>();
builder.Services.AddScoped<ICacheEstadoOcularPort, RedisEstadoOcularAdapter>();
builder.Services.AddScoped<IValidator<TelemetriaOcularEntradaDto>, TelemetriaOcularValidator>();
builder.Services.AddScoped<IProcesarFotogramaUseCase, ProcesarFotogramaUseCase>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("PoliticaMonitoreo", opt =>
    {
        opt.PermitLimit = 120;
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");

app.MapPost("/api/v1/monitoreo/evaluar", async (
    TelemetriaOcularEntradaDto dto,
    IProcesarFotogramaUseCase useCase,
    HttpContext http,
    CancellationToken ct) =>
{
    var resultado = await useCase.EjecutarAsync(dto, ct);
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";

    var respuestaHateoas = new
    {
        datos = resultado,
        _links = new
        {
            self = new { href = $"{baseUrl}/api/v1/monitoreo/evaluar", method = "POST" },
            historial = new { href = $"{baseUrl}/api/v1/monitoreo/historial/{resultado.ConductorId}", method = "GET" },
            health = new { href = $"{baseUrl}/health", method = "GET" }
        }
    };

    return Results.Ok(respuestaHateoas);
}).RequireRateLimiting("PoliticaMonitoreo");

app.MapGet("/api/v1/monitoreo/historial/{conductorId:guid}", async (
    Guid conductorId,
    IProcesarFotogramaUseCase useCase,
    CancellationToken ct) =>
{
    var historial = await useCase.ObtenerHistorialCriticoAsync(conductorId, ct);
    return Results.Ok(historial);
});

app.Run();