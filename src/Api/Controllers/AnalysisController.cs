using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Core.Models;
using System.Security.Claims;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
// 1. ¡EL 'USING' CLAVE! Ahora sí está en el archivo correcto.
using VisioAnalytica.Api.Logging;

namespace VisioAnalytica.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    // 2. ¡CORREGIDO! Aplicamos el Constructor Principal (IDE0290)
    public class AnalysisController(
        IAnalysisService analysisService,
        ILogger<AnalysisController> logger) : ControllerBase
    {
        // 3. Los campos se inicializan desde el constructor principal
        // Esto corrige los warnings CS8618 (campos no inicializados)
        private readonly IAnalysisService _analysisService = analysisService;
        private readonly ILogger<AnalysisController> _logger = logger;

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

            // 4. Usando nuestro log de alto rendimiento
            _logger.AnalysisRequestReceived(userId);

            try
            {
                var result = await _analysisService.PerformSstAnalysisAsync(request, userId);

                if (result == null)
                {
                    _logger.AnalysisServiceReturnedNull(userId); // Log de alto rendimiento
                    return StatusCode(500, "El análisis no pudo ser completado.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.CatastrophicError(userId, ex); // Log de alto rendimiento
                return StatusCode(500, "Ocurrió un error interno en el servidor.");
            }
        }
    }
}