using Microsoft.EntityFrameworkCore;
using Somnolencia.Infrastructure.Adapters.Out.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextPool<SomnolenciaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();