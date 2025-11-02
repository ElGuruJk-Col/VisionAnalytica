using System.Threading;      // ¡Necesario para CancellationToken!
using System.Threading.Tasks; // ¡Necesario para Task!
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para un servicio de análisis de IA (el "trabajador" de plomería).
    /// Su única responsabilidad es tomar datos y llamar a una API externa.
    /// </summary>
    public interface IAiSstAnalyzer
    {
        /// <summary>
        /// Analiza una imagen usando un prompt.
        /// </summary>
        /// <param name="imageBytes">Los bytes puros de la imagen.</param>
        /// <param name="prompt">El prompt textual completo.</param>
        /// <param name="ct">Un token de cancelación (buena práctica).</param> // ¡PARÁMETRO AÑADIDO!
        /// <returns>El resultado estructurado (o null si falla).</returns>
        Task<SstAnalysisResult?> AnalyzeImageAsync(
            byte[] imageBytes,
            string prompt,
            CancellationToken ct = default); // ¡FIRMA ACTUALIZADA!
    }
}