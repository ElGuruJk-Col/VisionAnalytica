using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación de 'IAiSstAnalyzer' usando la API REST de Google Gemini (v4.2 - Límite de Tokens Aumentado).
    /// </summary>
    public class GeminiAnalyzer : IAiSstAnalyzer
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiAnalyzer> _logger;
        private readonly string _apiKey;
        private readonly string _apiUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            // Opciones de serialización (si son necesarias)
        };

        public GeminiAnalyzer(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _apiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Configuración 'Gemini:ApiKey' no encontrada.");

            _apiUrl = configuration["Gemini:EndpointTemplate"]
                ?? throw new InvalidOperationException("Configuración 'Gemini:ApiUrl_GenerateContent' no encontrada.");

            if (!_apiUrl.Contains("?key="))
            {
                _apiUrl += $"?key={_apiKey}";
            }

            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<SstAnalysisResult?> AnalyzeImageAsync(byte[] imageBytes, string prompt, CancellationToken ct = default)
        {
            _logger.LogInformation("Iniciando análisis de Gemini (v4.2).");

            var imageBase64 = Convert.ToBase64String(imageBytes);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new { inline_data = new { mime_type = "image/jpeg", data = imageBase64 } }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    topK = 1,
                    topP = 1,
                    // --- ¡FIX #1: AUMENTAR LÍMITE DE TOKENS! ---
                    // Subimos de 2048 a 8192 para evitar el 'MAX_TOKENS'
                    maxOutputTokens = 8192,
                },
                safetySettings = new[]
                {
                    new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                    new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" },
                }
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(_apiUrl, payload, _jsonOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló la petición HTTP a Gemini.");
                throw new InvalidOperationException("Error de conexión con el servicio de IA.", ex);
            }

            var respJson = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error de la API de Gemini (HTTP {StatusCode}): {Response}",
                    response.StatusCode, respJson);
                throw new InvalidOperationException($"El servicio de IA devolvió un error HTTP {response.StatusCode}. Detalles: {respJson}");
            }

            _logger.LogInformation("Respuesta de Gemini recibida (HTTP 200). Iniciando parsing defensivo v4.2.");

            return ParseGeminiResponse(respJson);
        }

        // --- ¡FIX #2: PARSER REFORZADO! ---
        private SstAnalysisResult? ParseGeminiResponse(string respJson)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(respJson);
                var root = jsonDoc.RootElement;

                // Verificación Defensiva #1: ¿Existe 'candidates'?
                if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    // Esto puede pasar si 'finishReason' es 'SAFETY'
                    var reason = "Razón desconocida";
                    if (root.TryGetProperty("promptFeedback", out var feedback) &&
                       feedback.TryGetProperty("safetyRatings", out var ratings) &&
                       ratings.GetArrayLength() > 0)
                    {
                        reason = ratings[0].GetProperty("category").GetString() ?? "SAFETY";
                    }
                    _logger.LogWarning("Respuesta de Gemini bloqueada por seguridad o vacía. Razón: {Reason}", reason);
                    throw new InvalidOperationException($"Análisis bloqueado por el filtro de seguridad de la IA: {reason}");
                }

                var firstCandidate = candidates[0];

                // --- ¡NUEVA VERIFICACIÓN DEFENSIVA #2! ---
                // Revisamos 'finishReason' ANTES de intentar leer el contenido.
                if (firstCandidate.TryGetProperty("finishReason", out var reasonElement))
                {
                    var finishReason = reasonElement.GetString();
                    if (finishReason == "MAX_TOKENS")
                    {
                        _logger.LogError("Error de Gemini: 'finishReason' es 'MAX_TOKENS'. El límite de {Limit} sigue siendo muy bajo.", 8192);
                        throw new InvalidOperationException($"La IA no pudo completar la respuesta (MAX_TOKENS). El límite de tokens de salida (8192) es muy bajo.");
                    }
                    if (finishReason != "STOP") // "STOP" es el único éxito
                    {
                        _logger.LogWarning("Respuesta de Gemini bloqueada o detenida. Razón: {Reason}", finishReason);
                        throw new InvalidOperationException($"Análisis bloqueado por la IA. Razón: {finishReason}");
                    }
                }
                // Si 'finishReason' es 'STOP' (o no existe), continuamos...

                // Verificación Defensiva #3: ¿Existe 'content', 'parts' y 'text'?
                if (!firstCandidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                {
                    _logger.LogWarning("Respuesta de Gemini no contiene 'content' o 'parts'. JSON: {Json}", respJson);
                    throw new InvalidOperationException("La respuesta de la IA no tiene un formato válido (faltan 'content' o 'parts').");
                }

                if (!parts[0].TryGetProperty("text", out var textElement))
                {
                    _logger.LogWarning("La primera parte de la respuesta de Gemini no es 'text'.");
                    throw new InvalidOperationException("La respuesta de la IA no es de tipo texto.");
                }

                var text = textElement.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("La IA devolvió un texto vacío.");
                    return new SstAnalysisResult { Hallazgos = [] };
                }

                _logger.LogInformation("Texto de IA extraído, deserializando a SstAnalysisResult...");

                var cleanedText = text.Trim().Trim('`', 'j', 's', 'o', 'n');

                return JsonSerializer.Deserialize<SstAnalysisResult>(cleanedText, _jsonOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error al deserializar la respuesta de Gemini (JSON mal formado): {Json}", respJson);
                throw new InvalidOperationException("Error crítico: La IA devolvió un JSON interno mal formado.", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error desconocido parseando la respuesta de Gemini: {Json}", respJson);
                throw;
            }
        }
    }
}