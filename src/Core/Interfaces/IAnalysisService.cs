using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Interfaz del "Cerebro" Orquestador de Análisis.
    /// Esta es la capa de lógica de negocio.
    /// Su responsabilidad es decidir *qué* prompt usar (basado en el DTO)
    /// y luego llamar al conector de IA (IAiSstAnalyzer) apropiado.
    /// </summary>
    public interface IAnalysisService
    {
        /// <summary>
        /// Orquesta un análisis de imagen SST.
        /// </summary>
        /// <param name="request">El DTO de solicitud (con imagen y lógica de prompt opcional).</param>
        /// <param name="userId">El ID del usuario autenticado (para auditoría y lógica de tenant).</param>
        /// <returns>El resultado del análisis estructurado (SstAnalysisResult).</returns>
        Task<SstAnalysisResult?> PerformSstAnalysisAsync(
            AnalysisRequestDto request,
            string userId);
    }
}