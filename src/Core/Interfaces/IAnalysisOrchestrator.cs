namespace VisioAnalytica.Core.Interfaces;

/// <summary>
/// Interfaz para orquestar el análisis de múltiples fotos en segundo plano.
/// </summary>
public interface IAnalysisOrchestrator
{
    /// <summary>
    /// Analiza las fotos seleccionadas de una inspección en segundo plano.
    /// </summary>
    Task AnalyzeInspectionPhotosAsync(Guid inspectionId, List<Guid> photoIds, Guid userId);
}

