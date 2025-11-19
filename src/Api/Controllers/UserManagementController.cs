using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VisioAnalytica.Core.Constants;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Api.Controllers
{
    /// <summary>
    /// Controlador para la gestión de usuarios.
    /// Permite crear, listar, actualizar y gestionar usuarios del sistema.
    /// Requiere autenticación y roles específicos según la operación.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación para todos los endpoints
    public class UserManagementController : ControllerBase
    {
        private readonly IUserManagementService _userManagementService;
        private readonly ILogger<UserManagementController> _logger;

        public UserManagementController(
            IUserManagementService userManagementService,
            ILogger<UserManagementController> logger)
        {
            _userManagementService = userManagementService;
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
        /// Crea un nuevo usuario con contraseña provisional.
        /// Solo SuperAdmin y Admin pueden crear usuarios.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult<CreateUserResponseDto>> CreateUser([FromBody] CreateUserRequestDto request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserOrgId = GetCurrentUserOrganizationId();

                // Validar que el Admin solo puede crear usuarios en su propia organización
                if (HasAnyRole(Roles.Admin) && currentUserOrgId.HasValue)
                {
                    if (request.OrganizationId != currentUserOrgId.Value)
                    {
                        return Forbid("No puedes crear usuarios en otras organizaciones.");
                    }
                }

                var result = await _userManagementService.CreateUserAsync(request, currentUserId);
                _logger.LogInformation("Usuario creado: {Email} por usuario {CreatedBy}", result.Email, currentUserId);
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
                _logger.LogError(ex, "Error al crear usuario");
                return StatusCode(500, "Error interno del servidor al crear el usuario.");
            }
        }

        /// <summary>
        /// Obtiene todos los usuarios de la organización del usuario autenticado.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult<IList<UserListDto>>> GetUsers([FromQuery] bool includeInactive = false)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var users = await _userManagementService.GetUsersByOrganizationAsync(currentUserOrgId.Value, includeInactive);
                
                var userDtos = users.Select(u => new UserListDto
                {
                    Id = u.Id,
                    Email = u.Email!,
                    FirstName = u.FirstName!,
                    LastName = u.LastName!,
                    Roles = _userManagementService.GetUserRolesAsync(u.Id).Result,
                    IsActive = u.IsActive,
                    MustChangePassword = u.MustChangePassword,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios");
                return StatusCode(500, "Error interno del servidor al obtener usuarios.");
            }
        }

        /// <summary>
        /// Obtiene un usuario por su ID.
        /// </summary>
        [HttpGet("{userId}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult<UserListDto>> GetUser(Guid userId)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("Usuario no encontrado.");
                }

                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (HasAnyRole(Roles.Admin) && currentUserOrgId.HasValue && user.OrganizationId != currentUserOrgId.Value)
                {
                    return Forbid("No puedes acceder a usuarios de otras organizaciones.");
                }

                var roles = await _userManagementService.GetUserRolesAsync(userId);
                var userDto = new UserListDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    FirstName = user.FirstName!,
                    LastName = user.LastName!,
                    Roles = roles,
                    IsActive = user.IsActive,
                    MustChangePassword = user.MustChangePassword,
                    CreatedAt = user.CreatedAt
                };

                return Ok(userDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuario {UserId}", userId);
                return StatusCode(500, "Error interno del servidor al obtener el usuario.");
            }
        }

        /// <summary>
        /// Asigna un rol a un usuario.
        /// </summary>
        [HttpPost("{userId}/roles/{roleName}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> AssignRole(Guid userId, string roleName)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("Usuario no encontrado.");
                }

                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (HasAnyRole(Roles.Admin) && currentUserOrgId.HasValue && user.OrganizationId != currentUserOrgId.Value)
                {
                    return Forbid("No puedes modificar usuarios de otras organizaciones.");
                }

                // Validar que no se asigne SuperAdmin (solo puede ser asignado manualmente)
                if (roleName == Roles.SuperAdmin && !HasAnyRole(Roles.SuperAdmin))
                {
                    return Forbid("Solo un SuperAdmin puede asignar el rol SuperAdmin.");
                }

                var result = await _userManagementService.AssignRoleAsync(userId, roleName);
                if (result)
                {
                    _logger.LogInformation("Rol {Role} asignado al usuario {UserId}", roleName, userId);
                    return Ok(new { message = $"Rol '{roleName}' asignado correctamente." });
                }

                return BadRequest("No se pudo asignar el rol.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al asignar rol {Role} al usuario {UserId}", roleName, userId);
                return StatusCode(500, "Error interno del servidor al asignar el rol.");
            }
        }

        /// <summary>
        /// Remueve un rol de un usuario.
        /// </summary>
        [HttpDelete("{userId}/roles/{roleName}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> RemoveRole(Guid userId, string roleName)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("Usuario no encontrado.");
                }

                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (HasAnyRole(Roles.Admin) && currentUserOrgId.HasValue && user.OrganizationId != currentUserOrgId.Value)
                {
                    return Forbid("No puedes modificar usuarios de otras organizaciones.");
                }

                // Validar que no se remueva SuperAdmin (solo puede ser removido manualmente)
                if (roleName == Roles.SuperAdmin && !HasAnyRole(Roles.SuperAdmin))
                {
                    return Forbid("Solo un SuperAdmin puede remover el rol SuperAdmin.");
                }

                var result = await _userManagementService.RemoveRoleAsync(userId, roleName);
                if (result)
                {
                    _logger.LogInformation("Rol {Role} removido del usuario {UserId}", roleName, userId);
                    return Ok(new { message = $"Rol '{roleName}' removido correctamente." });
                }

                return BadRequest("No se pudo remover el rol.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al remover rol {Role} del usuario {UserId}", roleName, userId);
                return StatusCode(500, "Error interno del servidor al remover el rol.");
            }
        }

        /// <summary>
        /// Activa o desactiva un usuario.
        /// </summary>
        [HttpPatch("{userId}/active")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult> SetActiveStatus(Guid userId, [FromBody] bool isActive)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("Usuario no encontrado.");
                }

                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (HasAnyRole(Roles.Admin) && currentUserOrgId.HasValue && user.OrganizationId != currentUserOrgId.Value)
                {
                    return Forbid("No puedes modificar usuarios de otras organizaciones.");
                }

                var result = await _userManagementService.SetUserActiveStatusAsync(userId, isActive);
                if (result)
                {
                    _logger.LogInformation("Usuario {UserId} {Status}", userId, isActive ? "activado" : "desactivado");
                    return Ok(new { message = $"Usuario {(isActive ? "activado" : "desactivado")} correctamente." });
                }

                return BadRequest("No se pudo actualizar el estado del usuario.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estado del usuario {UserId}", userId);
                return StatusCode(500, "Error interno del servidor al actualizar el estado del usuario.");
            }
        }

        /// <summary>
        /// Obtiene los roles de un usuario.
        /// </summary>
        [HttpGet("{userId}/roles")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult<IList<string>>> GetUserRoles(Guid userId)
        {
            try
            {
                var user = await _userManagementService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("Usuario no encontrado.");
                }

                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (HasAnyRole(Roles.Admin) && currentUserOrgId.HasValue && user.OrganizationId != currentUserOrgId.Value)
                {
                    return Forbid("No puedes acceder a usuarios de otras organizaciones.");
                }

                var roles = await _userManagementService.GetUserRolesAsync(userId);
                return Ok(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener roles del usuario {UserId}", userId);
                return StatusCode(500, "Error interno del servidor al obtener los roles.");
            }
        }

        /// <summary>
        /// Obtiene todos los usuarios con un rol específico en la organización.
        /// </summary>
        [HttpGet("by-role/{roleName}")]
        [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Admin}")]
        public async Task<ActionResult<IList<UserListDto>>> GetUsersByRole(string roleName, [FromQuery] bool includeInactive = false)
        {
            try
            {
                var currentUserOrgId = GetCurrentUserOrganizationId();
                if (!currentUserOrgId.HasValue)
                {
                    return BadRequest("No se pudo determinar la organización del usuario.");
                }

                var users = await _userManagementService.GetUsersByRoleAsync(currentUserOrgId.Value, roleName);
                
                if (!includeInactive)
                {
                    users = users.Where(u => u.IsActive).ToList();
                }

                var userDtos = users.Select(u => new UserListDto
                {
                    Id = u.Id,
                    Email = u.Email!,
                    FirstName = u.FirstName!,
                    LastName = u.LastName!,
                    Roles = _userManagementService.GetUserRolesAsync(u.Id).Result,
                    IsActive = u.IsActive,
                    MustChangePassword = u.MustChangePassword,
                    CreatedAt = u.CreatedAt
                }).ToList();

                return Ok(userDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener usuarios por rol {Role}", roleName);
                return StatusCode(500, "Error interno del servidor al obtener usuarios.");
            }
        }
    }
}

