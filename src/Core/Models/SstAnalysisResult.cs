using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Define la estructura de un único hallazgo de SST (v2.0)
    /// Este modelo ahora distingue entre acciones correctivas y preventivas,
    /// de acuerdo con los estándares profesionales de SST.
    /// </summary>
    public record HallazgoItem
    (
        [property: JsonPropertyName("Descripcion")]
        string Descripcion,

        [property: JsonPropertyName("NivelRiesgo")]
        string NivelRiesgo,

        // ¡CAMBIO CLAVE! Reemplazamos "SolucionPropuesta" por dos campos

        [property: JsonPropertyName("AccionCorrectiva")]
        string AccionCorrectiva, // La solución inmediata

        [property: JsonPropertyName("AccionPreventiva")]
        string AccionPreventiva  // La solución a largo plazo (la "causa raíz")
    );

    /// <summary>
    /// El objeto raíz del resultado del análisis de SST.
    /// </summary>
    public record SstAnalysisResult
    {
        /// <summary>
        /// La lista de hallazgos identificados por la IA.
        /// </summary>
        [JsonPropertyName("Hallazgos")]
        public List<HallazgoItem> Hallazgos { get; init; } = [];
    }
}