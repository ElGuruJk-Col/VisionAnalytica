// En: src/Api/Controllers/AnalysisController.cs
// (v5.0 - REFACTORIZACIÓN FINAL Y FIX IDE0059)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Api.Logging;

namespace VisioAnalytica.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize] // ¡Todo este controlador requiere un Token JWT válido!
    public class AnalysisController(
        IAnalysisService analysisService,
        IReportService reportService,
        ILogger<AnalysisController> logger) : ControllerBase
    {
        private readonly IAnalysisService _analysisService = analysisService;
        private readonly IReportService _reportService = reportService;
        private readonly ILogger<AnalysisController> _logger = logger;

        // ===============================================
        // --- MÉTODO PRIVADO: FIX ARQUITECTÓNICO ---
        // ===============================================

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

            // Fix IDE0059: Si TryParse tiene éxito, el valor se asigna 
            // y se devuelve en el mismo flujo. Si falla, devuelve null, 
            // evitando la asignación redundante en el flujo de error.
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
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PerformSstAnalysis([FromBody] AnalysisRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized();
            }

            _logger.AnalysisRequestReceived(userId);

            try
            {
                // TO-DO: Aquí se llamará al servicio con el OrganizationId cuando se implemente la extracción
                var result = await _analysisService.PerformSstAnalysisAsync(request, userId);

                if (result == null)
                {
                    _logger.AnalysisServiceReturnedNull(userId);
                    return StatusCode(500, "El análisis no pudo ser completado.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.CatastrophicError(userId, ex);
                return StatusCode(500, "Ocurrió un error interno en el servidor.");
            }
        }

        // ===============================================
        // --- 2. ENDPOINTS DE LECTURA (GET) ---
        // ===============================================

        /// <summary>
        /// Obtiene el historial resumido de inspecciones para la organización del usuario.
        /// (Implementa filtro Multi-Tenant extrayendo el ID del Token JWT).
        /// </summary>
        [HttpGet("history")]
        [ProducesResponseType(typeof(IReadOnlyList<InspectionSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetInspectionHistory()
        {
            // --- ¡USO DEL HELPER PARA LIMPIAR EL CÓDIGO! ---
            var organizationId = GetOrganizationIdFromClaims();

            if (!organizationId.HasValue)
            {
                return Unauthorized("Token JWT inválido o sin ID de organización.");
            }
            // ------------------------------------------------

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
                return StatusCode(500, "Ocurrió un error interno consultando el historial.");
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
            // --- ¡USO DEL HELPER PARA LIMPIAR EL CÓDIGO! ---
            var organizationId = GetOrganizationIdFromClaims();

            if (!organizationId.HasValue)
            {
                return Unauthorized("Token JWT inválido o sin ID de organización.");
            }
            // ------------------------------------------------

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
                return StatusCode(500, "Ocurrió un error interno consultando el detalle.");
            }
        }
    }
}