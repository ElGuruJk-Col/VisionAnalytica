// En: src/Core/Interfaces/IReportService.cs
// (¡NUEVO ARCHIVO: Contrato de Reportes!)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisioAnalytica.Core.Models.Dtos; // Usamos los DTOs que acabamos de crear

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para el servicio de reportes y consultas históricas.
    /// Esta es la capa de negocio que orquesta la lectura de datos
    /// para los clientes (Web y Móvil).
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Obtiene un resumen del historial de inspecciones por organización.
        /// (Implementa filtro Multi-Tenant y paginación a futuro).
        /// </summary>
        /// <param name="organizationId">ID de la organización autenticada.</param>
        /// <returns>Una lista de DTOs resumidos.</returns>
        Task<IReadOnlyList<InspectionSummaryDto>> GetInspectionHistoryAsync(Guid organizationId);

        /// <summary>
        /// Obtiene el detalle completo de una inspección específica,
        /// incluyendo todos sus hallazgos.
        /// </summary>
        /// <param name="inspectionId">ID de la inspección.</param>
        /// <returns>El DTO de detalle.</returns>
        Task<InspectionDetailDto?> GetInspectionDetailsAsync(Guid inspectionId);
    }
}