using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Api.Controllers;

/// <summary>
/// Controlador para gestionar inspecciones con múltiples fotos.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class InspectionController : ControllerBase
{
    private readonly IInspectionService _inspectionService;
    private readonly ILogger<InspectionController> _logger;

    public InspectionController(
        IInspectionService inspectionService,
        ILogger<InspectionController> logger)
    {
        _inspectionService = inspectionService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene el ID del usuario autenticado desde el token JWT.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("uid")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }

    /// <summary>
    /// Obtiene el GUID de la organización del token JWT.
    /// </summary>
    private Guid? GetOrganizationIdFromClaims()
    {
        var orgIdString = User.FindFirst("org_id")?.Value;
        if (string.IsNullOrWhiteSpace(orgIdString) || !Guid.TryParse(orgIdString, out var organizationId))
        {
            return null;
        }
        return organizationId;
    }

    /// <summary>
    /// Crea una nueva inspección con múltiples fotos.
    /// </summary>
    [HttpPost("create")]
    [ProducesResponseType(typeof(InspectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InspectionDto>> CreateInspection([FromBody] CreateInspectionDto request)
    {
        var requestId = Guid.NewGuid();
        var userId = GetCurrentUserId();
        var organizationId = GetOrganizationIdFromClaims();
        
        _logger.LogInformation("🔵 [Controller] CreateInspection llamado - RequestId: {RequestId}, UserId: {UserId}, OrgId: {OrgId}, Fotos: {PhotoCount}, Time: {Time}", 
            requestId, userId, organizationId, request.Photos?.Count ?? 0, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));

        if (!userId.HasValue || !organizationId.HasValue)
        {
            _logger.LogWarning("❌ [Controller] CreateInspection - Usuario no autenticado - RequestId: {RequestId}", requestId);
            return Unauthorized("Usuario no autenticado o token inválido.");
        }

        if (request.Photos == null || request.Photos.Count == 0)
        {
            _logger.LogWarning("❌ [Controller] CreateInspection - Sin fotos - RequestId: {RequestId}", requestId);
            return BadRequest("Debe proporcionar al menos una foto.");
        }

        try
        {
            _logger.LogInformation("🟢 [Controller] Creando inspección - RequestId: {RequestId}, EmpresaId: {CompanyId}", requestId, request.AffiliatedCompanyId);
            var inspection = await _inspectionService.CreateInspectionAsync(
                request, userId.Value, organizationId.Value);
            
            _logger.LogInformation("✅ [Controller] Inspección creada - RequestId: {RequestId}, InspectionId: {InspectionId}", requestId, inspection.Id);
            return Ok(inspection);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Error al crear inspección para usuario {UserId}", userId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al crear inspección para usuario {UserId}", userId);
            return StatusCode(500, new { message = "Error interno del servidor." });
        }
    }

    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado.
    /// </summary>
    [HttpGet("my-inspections")]
    [ProducesResponseType(typeof(List<InspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<InspectionDto>>> GetMyInspections([FromQuery] Guid? affiliatedCompanyId = null)
    {
        var userId = GetCurrentUserId();
        var organizationId = GetOrganizationIdFromClaims();

        if (!userId.HasValue || !organizationId.HasValue)
        {
            return Unauthorized("Usuario no autenticado o token inválido.");
        }

        try
        {
            var inspections = await _inspectionService.GetMyInspectionsAsync(
                userId.Value, organizationId.Value, affiliatedCompanyId);
            
            _logger.LogInformation(
                "Retornando {Count} inspecciones para usuario {UserId}. Filtro empresa: {CompanyId}",
                inspections.Count, userId.Value, affiliatedCompanyId);
            
            // Log detallado de cada inspección
            foreach (var inspection in inspections)
            {
                _logger.LogDebug(
                    "Inspección {InspectionId}: {PhotosCount} fotos, {AnalyzedCount} analizadas, Estado: {Status}",
                    inspection.Id, inspection.PhotosCount, inspection.AnalyzedPhotosCount, inspection.Status);
            }
            
            return Ok(inspections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inspecciones para usuario {UserId}", userId);
            return StatusCode(500, new { message = "Error interno del servidor." });
        }
    }
    
    /// <summary>
    /// Obtiene las inspecciones del usuario autenticado con paginación.
    /// </summary>
    [HttpGet("my-inspections/paged")]
    [ProducesResponseType(typeof(PagedResult<InspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<InspectionDto>>> GetMyInspectionsPaged(
        [FromQuery] int pageNumber = 1, 
        [FromQuery] int pageSize = 20, 
        [FromQuery] Guid? affiliatedCompanyId = null)
    {
        var userId = GetCurrentUserId();
        var organizationId = GetOrganizationIdFromClaims();

        if (!userId.HasValue || !organizationId.HasValue)
        {
            return Unauthorized("Usuario no autenticado o token inválido.");
        }

        try
        {
            var result = await _inspectionService.GetMyInspectionsPagedAsync(
                userId.Value, 
                organizationId.Value, 
                pageNumber, 
                pageSize, 
                affiliatedCompanyId);
            
            _logger.LogInformation(
                "Retornando página {PageNumber} de {TotalPages} para usuario {UserId}. Items: {ItemCount}/{TotalCount}",
                result.PageNumber, result.TotalPages, userId.Value, result.Items.Count, result.TotalCount);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inspecciones paginadas para usuario {UserId}", userId);
            return StatusCode(500, new { message = "Error interno del servidor." });
        }
    }

    /// <summary>
    /// Obtiene los detalles de una inspección específica.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InspectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InspectionDto>> GetInspection(Guid id)
    {
        var userId = GetCurrentUserId();
        var organizationId = GetOrganizationIdFromClaims();

        if (!userId.HasValue || !organizationId.HasValue)
        {
            return Unauthorized("Usuario no autenticado o token inválido.");
        }

        try
        {
            var inspection = await _inspectionService.GetInspectionByIdAsync(
                id, userId.Value, organizationId.Value);

            if (inspection == null)
            {
                return NotFound(new { message = "Inspección no encontrada." });
            }

            return Ok(inspection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener inspección {InspectionId} para usuario {UserId}", id, userId);
            return StatusCode(500, new { message = "Error interno del servidor." });
        }
    }

    /// <summary>
    /// Inicia el análisis en segundo plano de las fotos seleccionadas.
    /// </summary>
    [HttpPost("{id:guid}/analyze")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> StartAnalysis(Guid id, [FromBody] AnalyzeInspectionDto request)
    {
        var userId = GetCurrentUserId();
        var organizationId = GetOrganizationIdFromClaims();

        if (!userId.HasValue || !organizationId.HasValue)
        {
            return Unauthorized("Usuario no autenticado o token inválido.");
        }

        if (request.InspectionId != id)
        {
            return BadRequest(new { message = "El ID de la inspección no coincide." });
        }

        if (request.PhotoIds == null || request.PhotoIds.Count == 0)
        {
            return BadRequest(new { message = "Debe seleccionar al menos una foto para analizar." });
        }

        try
        {
            var jobId = await _inspectionService.StartAnalysisAsync(
                request, userId.Value, organizationId.Value);
            
            return Ok(new { jobId, message = "Análisis iniciado en segundo plano." });
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { message = "El análisis en segundo plano aún no está implementado." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar análisis para inspección {InspectionId}", id);
            return StatusCode(500, new { message = "Error interno del servidor." });
        }
    }

    /// <summary>
    /// Obtiene el estado del análisis de una inspección.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(InspectionAnalysisStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InspectionAnalysisStatusDto>> GetAnalysisStatus(Guid id)
    {
        var userId = GetCurrentUserId();
        var organizationId = GetOrganizationIdFromClaims();

        if (!userId.HasValue || !organizationId.HasValue)
        {
            return Unauthorized("Usuario no autenticado o token inválido.");
        }

        try
        {
            var status = await _inspectionService.GetAnalysisStatusAsync(
                id, userId.Value, organizationId.Value);
            
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener estado de análisis para inspección {InspectionId}", id);
            return StatusCode(500, new { message = "Error interno del servidor." });
        }
    }

    /// <summary>
    /// Obtiene los hallazgos de una inspección de análisis (generada por una foto).
    /// </summary>
    [HttpGet("{id:guid}/findings")]
    [ProducesResponseType(typeof(List<FindingDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<FindingDetailDto>>> GetInspectionFindings(Guid id)
    {
        var userId = GetCurrentUserId();
        var organizationId = GetOrganizationIdFromClaims();

        if (!userId.HasValue || !organizationId.HasValue)
        {
            return Unauthorized("Usuario no autenticado o token inválido.");
        }

        try
        {
            var findings = await _inspectionService.GetInspectionFindingsAsync(
                id, userId.Value, organizationId.Value);
            
            return Ok(findings);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener hallazgos para inspección {InspectionId}", id);
            return StatusCode(500, new { message = "Error interno del servidor." });
        }
    }
}

