using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Somnolencia.WebApi.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public GlobalExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            var respuesta = new
            {
                codigo = 400,
                mensaje = "Error de validacion defensiva en los datos de entrada.",
                errores = ex.Errors
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(respuesta));
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var respuesta = new
            {
                codigo = 500,
                mensaje = "Error interno procesado por el Middleware Global.",
                detalle = ex.Message
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(respuesta));
        }
    }
}