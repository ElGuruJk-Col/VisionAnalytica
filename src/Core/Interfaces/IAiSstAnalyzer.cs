using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato (Interfaz) para cualquier servicio
    /// que analice imágenes de SST.
    /// Esta es la clave de nuestra estrategia de "IA Abstraída".
    /// </summary>
    public interface IAiSstAnalyzer
    {
        /// <summary>
        /// Analiza una imagen y devuelve un resultado estructurado.
        /// </summary>
        /// <param name="imageBytes">La imagen como un array de bytes.</param>
        /// <param name="prompt">El prompt de sistema que guía a la IA.</param>
        /// <returns>Un objeto SstAnalysisResult con los hallazgos.</returns>
        Task<SstAnalysisResult> AnalyzeImageAsync(byte[] imageBytes, string prompt);
    }
}

