using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Constants;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Api.Controllers;

/// <summary>
/// Controller para la configuración inicial del sistema.
/// Solo debe usarse para crear el primer SuperAdmin.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly VisioAnalyticaDbContext _context;
    private readonly ILogger<SetupController> _logger;

    public SetupController(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        VisioAnalyticaDbContext context,
        ILogger<SetupController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Inicializa el primer SuperAdmin del sistema.
    /// Solo funciona si no existe ningún SuperAdmin.
    /// </summary>
    [HttpPost("initialize-superadmin")]
    [AllowAnonymous] // Permitir sin autenticación solo para el setup inicial
    public async Task<IActionResult> InitializeSuperAdmin([FromBody] InitializeSuperAdminDto request)
    {
        try
        {
            // 1. Verificar que NO existe ningún SuperAdmin
            var existingSuperAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
            if (existingSuperAdmins.Any())
            {
                _logger.LogWarning("Intento de inicializar SuperAdmin cuando ya existe uno en el sistema.");
                return BadRequest(new { 
                    message = "El sistema ya tiene un SuperAdmin. Este endpoint está deshabilitado por seguridad.",
                    error = "SuperAdmin already exists"
                });
            }

            // 2. Verificar que el rol SuperAdmin existe
            var roleExists = await _roleManager.RoleExistsAsync(Roles.SuperAdmin);
            if (!roleExists)
            {
                return BadRequest(new { 
                    message = "El rol SuperAdmin no existe. Ejecuta primero el RoleSeeder.",
                    error = "Role not found"
                });
            }

            // 3. Crear o obtener la organización de VisioAnalytica
            var visioOrg = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Name == "VisioAnalytica");
            
            if (visioOrg == null)
            {
                visioOrg = new Organization
                {
                    Name = "VisioAnalytica"
                };
                _context.Organizations.Add(visioOrg);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Organización 'VisioAnalytica' creada.");
            }

            // 4. Verificar que el email no esté en uso
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { 
                    message = $"El email {request.Email} ya está en uso.",
                    error = "Email already exists"
                });
            }

            // 5. Crear el usuario SuperAdmin
            // Nota: UserManager.CreateAsync validará automáticamente la contraseña según las opciones configuradas
            var superAdmin = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email.ToLower(),
                UserName = request.Email.ToLower(),
                OrganizationId = visioOrg.Id,
                IsActive = true,
                MustChangePassword = true, // Forzar cambio de contraseña en primer login
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(superAdmin, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogError("Error al crear SuperAdmin: {Errors}", errors);
                return BadRequest(new { 
                    message = $"Error al crear el usuario: {errors}",
                    error = "User creation failed",
                    details = createResult.Errors.Select(e => new { e.Code, e.Description })
                });
            }

            // 6. Asignar rol SuperAdmin
            var roleResult = await _userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
            if (!roleResult.Succeeded)
            {
                // Si falla la asignación de rol, eliminar el usuario
                await _userManager.DeleteAsync(superAdmin);
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Error al asignar rol SuperAdmin: {Errors}", errors);
                return BadRequest(new { 
                    message = $"Error al asignar el rol: {errors}",
                    error = "Role assignment failed"
                });
            }

            _logger.LogInformation("SuperAdmin creado exitosamente: {Email} (ID: {UserId})", superAdmin.Email, superAdmin.Id);

            return Ok(new { 
                message = "SuperAdmin creado exitosamente.",
                userId = superAdmin.Id,
                email = superAdmin.Email,
                organizationId = visioOrg.Id,
                warning = "⚠️ IMPORTANTE: Cambia la contraseña después del primer login."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al inicializar SuperAdmin");
            return StatusCode(500, new { 
                message = "Error interno del servidor al inicializar SuperAdmin.",
                error = "Internal server error"
            });
        }
    }

    /// <summary>
    /// Verifica si el sistema ya tiene un SuperAdmin configurado.
    /// Útil para verificar el estado del sistema antes de intentar inicializar.
    /// </summary>
    [HttpGet("check-status")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckSetupStatus()
    {
        try
        {
            var existingSuperAdmins = await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
            var hasSuperAdmin = existingSuperAdmins.Any();
            var roleExists = await _roleManager.RoleExistsAsync(Roles.SuperAdmin);
            var visioOrgExists = await _context.Organizations
                .AnyAsync(o => o.Name == "VisioAnalytica");

            return Ok(new
            {
                isInitialized = hasSuperAdmin,
                hasSuperAdmin = hasSuperAdmin,
                roleExists = roleExists,
                organizationExists = visioOrgExists,
                message = hasSuperAdmin 
                    ? "El sistema ya está inicializado. El endpoint de setup está deshabilitado."
                    : "El sistema no está inicializado. Puedes usar /api/setup/initialize-superadmin para crear el primer SuperAdmin."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar estado del setup");
            return StatusCode(500, new { 
                message = "Error al verificar el estado del sistema.",
                error = "Internal server error"
            });
        }
    }
}

/// <summary>
/// DTO para inicializar el SuperAdmin.
/// </summary>
public record InitializeSuperAdminDto(
    string Email,
    string Password,
    string FirstName,
    string LastName
);

