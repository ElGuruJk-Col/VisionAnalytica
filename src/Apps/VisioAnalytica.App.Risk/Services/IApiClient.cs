using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Interfaz para el cliente HTTP que se comunica con la API backend.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Realiza una petición GET a la API.
    /// </summary>
    Task<T?> GetAsync<T>(string endpoint) where T : class;

    /// <summary>
    /// Realiza una petición POST a la API.
    /// </summary>
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest request) where TResponse : class;

    /// <summary>
    /// Establece el token JWT para las peticiones autenticadas.
    /// </summary>
    void SetAuthToken(string? token);

    /// <summary>
    /// Obtiene la URL base de la API.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Obtiene las empresas cliente asignadas al inspector autenticado.
    /// </summary>
    Task<IList<VisioAnalytica.Core.Models.Dtos.AffiliatedCompanyDto>> GetMyCompaniesAsync(bool includeInactive = false);

    /// <summary>
    /// Obtiene todas las empresas afiliadas de la organización del usuario autenticado (para SuperAdmin/Admin).
    /// </summary>
    Task<IList<VisioAnalytica.Core.Models.Dtos.AffiliatedCompanyDto>> GetAllCompaniesAsync(bool includeInactive = false);

    /// <summary>
    /// Notifica al supervisor que el inspector no tiene empresas asignadas.
    /// </summary>
    Task<bool> NotifyInspectorWithoutCompaniesAsync();

    /// <summary>
    /// Crea una nueva inspección con múltiples fotos.
    /// </summary>
    Task<InspectionDto?> CreateInspectionAsync(CreateInspectionDto request);

    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado.
    /// </summary>
    Task<List<InspectionDto>> GetMyInspectionsAsync(Guid? affiliatedCompanyId = null);
    
    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado con paginación.
    /// </summary>
    Task<PagedResult<InspectionDto>> GetMyInspectionsPagedAsync(int pageNumber = 1, int pageSize = 20, Guid? affiliatedCompanyId = null);

    /// <summary>
    /// Obtiene los detalles de una inspección específica.
    /// </summary>
    Task<InspectionDto?> GetInspectionByIdAsync(Guid inspectionId);

    /// <summary>
    /// Inicia el análisis en segundo plano de las fotos seleccionadas.
    /// </summary>
    Task<string> StartAnalysisAsync(AnalyzeInspectionDto request);

    /// <summary>
    /// Obtiene el estado del análisis de una inspección.
    /// </summary>
    Task<InspectionAnalysisStatusDto> GetAnalysisStatusAsync(Guid inspectionId);

    /// <summary>
    /// Obtiene los hallazgos de una inspección de análisis (generada por una foto).
    /// </summary>
    Task<List<FindingDetailDto>> GetInspectionFindingsAsync(Guid analysisInspectionId);

    /// <summary>
    /// Obtiene el historial de inspecciones de la organización (para Admin/Supervisor).
    /// </summary>
    Task<List<InspectionSummaryDto>> GetOrganizationHistoryAsync();
}

