using System.Text.Json;
using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del servicio de análisis de imágenes.
/// </summary>
public class AnalysisService : IAnalysisService
{
    private readonly IApiClient _apiClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public AnalysisService(IApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<AnalysisResult?> AnalyzeImageAsync(byte[] imageBytes)
    {
        try
        {
            // Convertir imagen a Base64
            var imageBase64 = Convert.ToBase64String(imageBytes);

            // Crear request
            var request = new AnalysisRequest(imageBase64);

            // Llamar a la API
            var response = await _apiClient.PostAsync<AnalysisRequest, AnalysisResult>(
                "/api/v1/analysis/PerformSstAnalysis", request);

            return response;
        }
        catch (ApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ApiException($"Error al analizar imagen: {ex.Message}", ex);
        }
    }
}

