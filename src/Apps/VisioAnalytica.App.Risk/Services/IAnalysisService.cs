using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Interfaz para el servicio de análisis de imágenes.
/// </summary>
public interface IAnalysisService
{
    /// <summary>
    /// Realiza un análisis de imagen SST.
    /// </summary>
    Task<AnalysisResult?> AnalyzeImageAsync(byte[] imageBytes);
}

