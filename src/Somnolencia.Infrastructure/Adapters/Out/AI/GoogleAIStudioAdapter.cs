using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Somnolencia.Application.Ports.Out;

namespace Somnolencia.Infrastructure.Adapters.Out.AI;

public sealed class GoogleAIStudioAdapter : IAnalizadorSomnolenciaIAPort
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public GoogleAIStudioAdapter(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GEMINI_API_KEY"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    }

    public async Task<string> AnalizarFotogramaSomnolenciaAsync(
        string fotogramaBase64,
        double duracionCierreMs,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("pega_aqui"))
        {
            return $"Alerta confirmada por modelo local keras_model.h5 ({duracionCierreMs} ms con ojos cerrados).";
        }

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "image/jpeg",
                                data = fotogramaBase64
                            }
                        },
                        new
                        {
                            text = $"El modelo TensorFlow detecto ojos cerrados por {duracionCierreMs} ms. Analiza el rostro y responde brevemente el nivel de fatiga."
                        }
                    }
                }
            }
        };

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await _httpClient.PostAsync(endpoint, jsonContent, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}