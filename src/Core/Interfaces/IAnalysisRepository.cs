// En: src/Core/Interfaces/IAnalysisRepository.cs
// (¡NUEVO ARCHIVO: Contrato de Persistencia!)

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para la capa de persistencia de análisis.
    /// Su única responsabilidad es manejar las operaciones CRUD (Guardar, Leer, etc.)
    /// de las entidades Inspection y Finding en la base de datos.
    /// </summary>
    public interface IAnalysisRepository
    {
        /// <summary>
        /// Guarda una nueva Inspección y todos sus Hallazgos asociados.
        /// </summary>
        /// <param name="inspection">La Inspección a guardar (debe incluir los Findings).</param>
        /// <returns>La Inspección guardada con su ID generado.</returns>
        Task<Inspection> SaveInspectionAsync(Inspection inspection);

        /// <summary>
        /// Obtiene una Inspección completa por su ID.
        /// </summary>
        /// <param name="inspectionId">ID de la inspección.</param>
        /// <returns>La Inspección o null si no existe.</returns>
        Task<Inspection?> GetInspectionByIdAsync(Guid inspectionId);

        /// <summary>
        /// Obtiene una lista de inspecciones por el ID de la organización (Multi-Tenant).
        /// </summary>
        /// <param name="organizationId">ID de la organización.</param>
        /// <returns>Lista de Inspecciones, o lista vacía.</returns>
        Task<IReadOnlyList<Inspection>> GetInspectionsByOrganizationAsync(Guid organizationId);

        /// <summary>
        /// Obtiene el ID de la primera empresa afiliada activa para una organización.
        /// </summary>
        /// <param name="organizationId">ID de la organización.</param>
        /// <returns>ID de la empresa afiliada o null si no existe ninguna activa.</returns>
        Task<Guid?> GetFirstActiveAffiliatedCompanyIdAsync(Guid organizationId);
    }
}