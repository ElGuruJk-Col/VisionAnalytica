using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using System; // Necesario para Guid
using System.Threading.Tasks;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Interfaz del "Cerebro" Orquestador de Análisis.
    /// Esta es la capa de lógica de negocio.
    /// </summary>
    public interface IAnalysisService
    {
        /// <summary>
        /// Orquesta un análisis de imagen SST.
        /// </summary>
        /// <param name="request">El DTO de solicitud.</param>
        /// <param name="userId">El ID del usuario autenticado.</param>
        /// <param name="organizationId">El ID de la organización autenticada (para Multi-Tenant).</param>
        /// <returns>El resultado del análisis estructurado (SstAnalysisResult).</returns>
        Task<SstAnalysisResult?> PerformSstAnalysisAsync(
            AnalysisRequestDto request,
            string userId,
            Guid organizationId);
    }
}