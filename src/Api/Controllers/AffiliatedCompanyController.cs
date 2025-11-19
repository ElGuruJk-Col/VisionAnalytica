using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VisioAnalytica.Core.Constants;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Api.Controllers
{
    /// <summary>
    /// Controlador para la gestión de empresas afiliadas.
    /// Permite crear, listar, actualizar y gestionar empresas afiliadas y sus asignaciones de inspectores.
    /// Requiere autenticación y roles específicos según la operación.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación para todos los endpoints
    public class AffiliatedCompanyController : ControllerBase
    {
        private readonly IAffiliatedCompanyService _affiliatedCompanyService;
        private readonly ILogger<AffiliatedCompanyController> _logger;

        public AffiliatedCompanyController(
            IAffiliatedCompanyService affiliatedCompanyService,
            ILogger<AffiliatedCompanyController> logger)
        {
            _affiliatedCompanyService = affiliatedCompanyService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el ID del usuario autenticado desde el token JWT.
        /// </summary>
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("Usuario no autenticado o token inválido.");
            }
            return userId;
        }

        /// <summary>
        /// Obtiene el ID de la organización del usuario autenticado.
        /// </summary>
        private Guid? GetCurrentUserOrganizationId()
        {
            var orgIdClaim = User.FindFirst("organization_id")?.Value;
            if (string.IsNullOrEmpty(orgIdClaim) || !Guid.TryParse(orgIdClaim, out var orgId))
            {
                return null;
            }
            return orgId;
        }

        /// <summary>
        /// Verifica si el usuario tiene uno de los roles especificados.
        /// </summary>
        private bool HasAnyRole(params string[] roles)
        {
            return User.Claims.Any(c => c.Type == ClaimTypes.Role && roles.Contains(c.Value));
        }

        /// <summary>
        /// Crea una nueva empresa afiliada.
        /// Solo Admin puede crear empresas afiliadas.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult<AffiliatedCompanyDto>> Create([FromBody] CreateAffiliatedCompanyDto request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserOrgId = GetCurrentUserOrganizationId();

                // Validar que el Admin solo puede crear empresas en su propia organización
                if (HasAnyRole(Roles.Admin) && currentUserOrgId.HasValue)
                {
                    if (request.OrganizationId != currentUserOrgId.Value)
                    {
                        return Forbid("No puedes crear empresas afiliadas en otras organizaciones.");
                    }
                }

                var result = await _affiliatedCompanyService.CreateAsync(request, currentUserId);
                _logger.LogInformation("Empresa afiliada creada: {Name} (ID: {Id}) por usuario {CreatedBy}", 
                    result.Name, result.Id, currentUserId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear empresa afiliada");
                return StatusCode(500, "Error interno del servidor al crear la empresa afiliada.");
            }
        }

        /// <summary>
        /// Obtiene todas las empresas afiliadas de la organización del usuario autenticado.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin},{Roles.Inspector},{Roles.Cliente}")]
        public async Task<ActionResult<IList<AffiliatedCompanyDto>>> GetAll([FromQuery] bool includeInactive = false)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var companies = await _affiliatedCompanyService.GetByOrganizationAsync(currentUserOrgId.Value, includeInactive);
                return Ok(companies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empresas afiliadas");
                return StatusCode(500, "Error interno del servidor al obtener empresas afiliadas.");
            }
        }

        /// <summary>
        /// Obtiene una empresa afiliada por su ID.
        /// </summary>
        [HttpGet("{companyId}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin},{Roles.Inspector},{Roles.Cliente}")]
        public async Task<ActionResult<AffiliatedCompanyDto>> GetById(Guid companyId)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var company = await _affiliatedCompanyService.GetByIdAsync(companyId, currentUserOrgId.Value);
                if (company == null)
                {
                    return NotFound("Empresa afiliada no encontrada.");
                }

                return Ok(company);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empresa afiliada {CompanyId}", companyId);
                return StatusCode(500, "Error interno del servidor al obtener la empresa afiliada.");
            }
        }

        /// <summary>
        /// Actualiza una empresa afiliada.
        /// </summary>
        [HttpPut("{companyId}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult<AffiliatedCompanyDto>> Update(Guid companyId, [FromBody] UpdateAffiliatedCompanyDto request)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var result = await _affiliatedCompanyService.UpdateAsync(companyId, request);
                _logger.LogInformation("Empresa afiliada actualizada: {CompanyId}", companyId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar empresa afiliada {CompanyId}", companyId);
                return StatusCode(500, "Error interno del servidor al actualizar la empresa afiliada.");
            }
        }

        /// <summary>
        /// Activa o desactiva una empresa afiliada.
        /// </summary>
        [HttpPatch("{companyId}/active")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> SetActiveStatus(Guid companyId, [FromBody] bool isActive)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var result = await _affiliatedCompanyService.SetActiveStatusAsync(companyId, isActive, currentUserOrgId.Value);
                if (result)
                {
                    _logger.LogInformation("Empresa afiliada {CompanyId} {Status}", companyId, isActive ? "activada" : "desactivada");
                    return Ok(new { message = $"Empresa afiliada {(isActive ? "activada" : "desactivada")} correctamente." });
                }

                return BadRequest("No se pudo actualizar el estado de la empresa afiliada.");
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estado de empresa afiliada {CompanyId}", companyId);
                return StatusCode(500, "Error interno del servidor al actualizar el estado.");
            }
        }

        /// <summary>
        /// Elimina una empresa afiliada (soft delete).
        /// </summary>
        [HttpDelete("{companyId}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> Delete(Guid companyId)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var result = await _affiliatedCompanyService.DeleteAsync(companyId, currentUserOrgId.Value);
                if (result)
                {
                    _logger.LogInformation("Empresa afiliada eliminada: {CompanyId}", companyId);
                    return Ok(new { message = "Empresa afiliada eliminada correctamente." });
                }

                return BadRequest("No se pudo eliminar la empresa afiliada.");
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar empresa afiliada {CompanyId}", companyId);
                return StatusCode(500, "Error interno del servidor al eliminar la empresa afiliada.");
            }
        }

        /// <summary>
        /// Asigna un inspector a una empresa afiliada.
        /// </summary>
        [HttpPost("{companyId}/inspectors/{inspectorId}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> AssignInspector(Guid companyId, Guid inspectorId)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var result = await _affiliatedCompanyService.AssignInspectorAsync(companyId, inspectorId, currentUserOrgId.Value);
                if (result)
                {
                    _logger.LogInformation("Inspector {InspectorId} asignado a empresa {CompanyId}", inspectorId, companyId);
                    return Ok(new { message = "Inspector asignado correctamente." });
                }

                return BadRequest("No se pudo asignar el inspector.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar inspector {InspectorId} a empresa {CompanyId}", inspectorId, companyId);
                return StatusCode(500, "Error interno del servidor al asignar el inspector.");
            }
        }

        /// <summary>
        /// Remueve un inspector de una empresa afiliada.
        /// </summary>
        [HttpDelete("{companyId}/inspectors/{inspectorId}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> RemoveInspector(Guid companyId, Guid inspectorId)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var result = await _affiliatedCompanyService.RemoveInspectorAsync(companyId, inspectorId, currentUserOrgId.Value);
                if (result)
                {
                    _logger.LogInformation("Inspector {InspectorId} removido de empresa {CompanyId}", inspectorId, companyId);
                    return Ok(new { message = "Inspector removido correctamente." });
                }

                return BadRequest("No se pudo remover el inspector.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al remover inspector {InspectorId} de empresa {CompanyId}", inspectorId, companyId);
                return StatusCode(500, "Error interno del servidor al remover el inspector.");
            }
        }

        /// <summary>
        /// Obtiene todas las empresas afiliadas asignadas al inspector autenticado.
        /// </summary>
        [HttpGet("my-companies")]
        [Authorize(Roles = Roles.Inspector)]
        public async Task<ActionResult<IList<AffiliatedCompanyDto>>> GetMyCompanies([FromQuery] bool includeInactive = false)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var companies = await _affiliatedCompanyService.GetByInspectorAsync(currentUserId, includeInactive);
                return Ok(companies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener empresas del inspector {InspectorId}", GetCurrentUserId());
                return StatusCode(500, "Error interno del servidor al obtener empresas.");
            }
        }

        /// <summary>
        /// Obtiene todos los inspectores asignados a una empresa afiliada.
        /// </summary>
        [HttpGet("{companyId}/inspectors")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> GetAssignedInspectors(Guid companyId)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var inspectors = await _affiliatedCompanyService.GetAssignedInspectorsAsync(companyId, currentUserOrgId.Value);
                var inspectorDtos = inspectors.Select(i => new
                {
                    Id = i.Id,
                    Email = i.Email,
                    FirstName = i.FirstName,
                    LastName = i.LastName,
                    FullName = $"{i.FirstName} {i.LastName}",
                    IsActive = i.IsActive
                }).ToList();

                return Ok(inspectorDtos);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener inspectores de empresa {CompanyId}", companyId);
                return StatusCode(500, "Error interno del servidor al obtener inspectores.");
            }
        }
    }
}

