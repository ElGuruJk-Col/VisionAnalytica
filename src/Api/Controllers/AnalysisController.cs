using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VisioAnalytica.Api.Logging;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    // Usamos el Constructor Principal (moderno y limpio)
    public class AnalysisController(
        IAnalysisService analysisService,
        IReportService reportService,
        ILogger<AnalysisController> logger) : ControllerBase
    {
        private readonly IAnalysisService _analysisService = analysisService;
        private readonly IReportService _reportService = reportService;
        private readonly ILogger<AnalysisController> _logger = logger;

        /// <summary>
        /// Intenta obtener el GUID de la organización del token JWT (claim "org_id").
        /// Si falla (null, whitespace, o formato incorrecto), devuelve null.
        /// </summary>
        private Guid? GetOrganizationIdFromClaims()
        {
            var orgIdString = User.FindFirstValue("org_id");

            if (string.IsNullOrWhiteSpace(orgIdString))
            {
                return null;
            }

            if (Guid.TryParse(orgIdString, out var organizationId))
            {
                return organizationId;
            }

            return null;
        }

        // ===============================================
        // --- 1. ENDPOINT DE ESCRITURA (POST) ---
        // ===============================================

        [HttpPost("PerformSstAnalysis")]
        [ProducesResponseType(typeof(SstAnalysisResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PerformSstAnalysis([FromBody] AnalysisRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Usamos 'uid' que es donde el TokenService guarda el ID
            var userId = User.FindFirstValue("uid");
            var organizationId = GetOrganizationIdFromClaims();

            // Validación de seguridad
            if (string.IsNullOrWhiteSpace(userId) || !organizationId.HasValue)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "El identificador de usuario (uid) o de organización (org_id) está ausente o es inválido en el token.");
            }

            _logger.AnalysisRequestReceived(userId);

            try
            {
                // Ahora la firma del servicio toma 3 argumentos.
                var result = await _analysisService.PerformSstAnalysisAsync(request, userId, organizationId.Value);

                if (result == null)
                {
                    _logger.AnalysisServiceReturnedNull(userId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "El análisis no pudo ser completado.");
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                // Capturamos el error de formato GUID o de prompt
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.CatastrophicError(userId, ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error interno en el servidor.");
            }
        }

        // ===============================================
        // --- 2. ENDPOINTS DE LECTURA (GET) ---
        // ===============================================

        /// <summary>
        /// Obtiene el historial resumido de inspecciones para la organización del usuario.
        /// </summary>
        
        
        [HttpGet("history")]
        [ProducesResponseType(typeof(IReadOnlyList<InspectionSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetInspectionHistory()
        {
            var organizationId = GetOrganizationIdFromClaims();

            if (!organizationId.HasValue)
            {
                return Unauthorized("Token JWT inválido o sin ID de organización.");
            }

            try
            {
                var result = await _reportService.GetInspectionHistoryAsync(organizationId.Value);

                if (result == null || result.Count == 0)
                {
                    return Ok(Array.Empty<InspectionSummaryDto>());
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando el historial para la Organización {OrganizationId}.", organizationId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error interno consultando el historial.");
            }
        }

        /// <summary>
        /// Obtiene el detalle completo de una inspección específica.
        /// </summary>
        /// <param name="inspectionId">El ID de la inspección a consultar.</param>
        [HttpGet("{inspectionId:guid}")]
        [ProducesResponseType(typeof(InspectionDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetInspectionDetails(Guid inspectionId)
        {
            var organizationId = GetOrganizationIdFromClaims();

            if (!organizationId.HasValue)
            {
                return Unauthorized("Token JWT inválido o sin ID de organización.");
            }

            try
            {
                var result = await _reportService.GetInspectionDetailsAsync(inspectionId);

                if (result == null)
                {
                    return NotFound($"Inspección con ID {inspectionId} no encontrada.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando detalles de inspección {InspectionId}.", inspectionId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Ocurrió un error interno consultando el detalle.");
            }
        }
    }
}