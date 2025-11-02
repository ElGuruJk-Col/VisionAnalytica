using System.ComponentModel.DataAnnotations; // Necesario

namespace VisioAnalytica.Core.Models.Dtos
{
    /// <summary>
    /// DTO para una solicitud de análisis de imagen SST. (v2.1 Corregido)
    /// Se eliminó el prefijo [property:] de los atributos de validación
    /// para cumplir con los requisitos del Model Binder de ASP.NET Core.
    /// </summary>
    /// <param name="ImageBase64">La imagen (Base64) - Siempre obligatoria.</param>
    /// <param name="PromptTemplateId">Opcional. ID de una plantilla de prompt.</param>
    /// <param name="CustomPrompt">Opcional. Un prompt personalizado.</param>
    public record AnalysisRequestDto
    (
        // ¡SIN 'property:'! El validador se asocia al PARÁMETRO.
        [Required(ErrorMessage = "La imagen en formato Base64 es obligatoria.")]
        string ImageBase64,

        int? PromptTemplateId,

        // ¡SIN 'property:'! El validador se asocia al PARÁMETRO.
        [StringLength(1000,
            ErrorMessage = "El prompt personalizado no puede exceder los 1000 caracteres.")]
        string? CustomPrompt
    );
}