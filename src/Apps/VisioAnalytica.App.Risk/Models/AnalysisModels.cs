namespace VisioAnalytica.App.Risk.Models;

/// <summary>
/// Modelo para solicitud de análisis.
/// </summary>
public record AnalysisRequest(
    string ImageBase64,
    int? PromptTemplateId = null,
    string? CustomPrompt = null
);

/// <summary>
/// Modelo de hallazgo individual.
/// </summary>
public record FindingItem(
    string Descripcion,
    string NivelRiesgo,
    string AccionCorrectiva,
    string AccionPreventiva
);

/// <summary>
/// Modelo de resultado de análisis.
/// </summary>
public record AnalysisResult
{
    /// <summary>
    /// La URL de la imagen analizada, una vez guardada en el almacenamiento.
    /// </summary>
    public string? ImageUrl { get; init; }

    public List<FindingItem> Hallazgos { get; init; } = new();
}

