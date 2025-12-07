using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Core.Interfaces;

/// <summary>
/// Interfaz para el servicio de gestión de inspecciones.
/// Maneja la creación de inspecciones, captura de fotos y análisis.
/// </summary>
public interface IInspectionService
{
    /// <summary>
    /// Crea una nueva inspección con múltiples fotos.
    /// </summary>
    Task<InspectionDto> CreateInspectionAsync(CreateInspectionDto request, Guid userId, Guid organizationId);

    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado.
    /// </summary>
    Task<List<InspectionDto>> GetMyInspectionsAsync(Guid userId, Guid organizationId, Guid? affiliatedCompanyId = null);
    
    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado con paginación.
    /// </summary>
    Task<PagedResult<InspectionDto>> GetMyInspectionsPagedAsync(
        Guid userId, 
        Guid organizationId, 
        int pageNumber = 1, 
        int pageSize = 20, 
        Guid? affiliatedCompanyId = null);

    /// <summary>
    /// Obtiene los detalles de una inspección específica.
    /// </summary>
    Task<InspectionDto?> GetInspectionByIdAsync(Guid inspectionId, Guid userId, Guid organizationId);

    /// <summary>
    /// Inicia el análisis en segundo plano de las fotos seleccionadas.
    /// </summary>
    Task<string> StartAnalysisAsync(AnalyzeInspectionDto request, Guid userId, Guid organizationId);

    /// <summary>
    /// Obtiene el estado del análisis de una inspección.
    /// </summary>
    Task<InspectionAnalysisStatusDto> GetAnalysisStatusAsync(Guid inspectionId, Guid userId, Guid organizationId);

    /// <summary>
    /// Obtiene los hallazgos de una inspección de análisis (generada por una foto).
    /// </summary>
    Task<List<FindingDetailDto>> GetInspectionFindingsAsync(Guid analysisInspectionId, Guid userId, Guid organizationId);
}

