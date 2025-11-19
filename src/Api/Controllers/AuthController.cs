using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisioAnalytica.Core.Interfaces; // ¡Importamos el nuevo contrato!
using VisioAnalytica.Core.Models.Dtos; // ¡Importamos los DTOs desde Core!

namespace VisioAnalytica.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(IAuthService authService) : ControllerBase
    {
        // --- ¡AHORA SOLO DEPENDE DE UN SERVICIO! ---
        private readonly IAuthService _authService = authService;

        // (Los DTOs ya NO viven aquí. Han sido movidos a Core/Models/Dtos)

        // --- Endpoint de Registro ---
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            try
            {
                // ¡Solo "pasa la pelota" al servicio!
                var userDto = await _authService.RegisterAsync(registerDto);
                return Ok(userDto);
            }
            catch (ArgumentException ex) // Ej. Email ya existe
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) // Ej. Password débil
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Error genérico (ej. la BBDD se cayó)
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // --- Endpoint de Login ---
        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            try
            {
                // ¡Solo "pasa la pelota" al servicio!
                var userDto = await _authService.LoginAsync(loginDto);
                return Ok(userDto);
            }
            catch (UnauthorizedAccessException ex) // Ej. Password incorrecto
            {
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // --- Endpoint de Cambio de Contraseña ---
        [HttpPost("change-password")]
        [Authorize] // Requiere autenticación
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            try
            {
                // Obtener el ID del usuario desde el token JWT
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized("Token inválido o usuario no autenticado.");
                }

                var result = await _authService.ChangePasswordAsync(userId, changePasswordDto);
                if (result)
                {
                    return Ok(new { message = "Contraseña cambiada correctamente." });
                }

                return BadRequest("No se pudo cambiar la contraseña.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        // --- Endpoint de Solicitud de Recuperación de Contraseña ---
        [HttpPost("forgot-password")]
        [AllowAnonymous] // No requiere autenticación
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            try
            {
                // Por seguridad, siempre devolvemos éxito aunque el email no exista
                await _authService.ForgotPasswordAsync(forgotPasswordDto);
                return Ok(new { message = "Si el email existe, recibirás instrucciones para restablecer tu contraseña." });
            }
            catch (Exception ex)
            {
                // Por seguridad, no revelamos errores específicos
                return Ok(new { message = "Si el email existe, recibirás instrucciones para restablecer tu contraseña." });
            }
        }

        // --- Endpoint de Restablecimiento de Contraseña ---
        [HttpPost("reset-password")]
        [AllowAnonymous] // No requiere autenticación
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            try
            {
                var result = await _authService.ResetPasswordAsync(resetPasswordDto);
                if (result)
                {
                    return Ok(new { message = "Contraseña restablecida correctamente." });
                }

                return BadRequest("No se pudo restablecer la contraseña. Verifica que el token sea válido y no haya expirado.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }
    }
}