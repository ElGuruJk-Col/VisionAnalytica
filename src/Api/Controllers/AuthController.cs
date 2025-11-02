// En: src/Api/Controllers/AuthController.cs
// (¡VERSIÓN 2.0 - 100% PURA Y REFACTORIZADA!)
// Tarea 30.E: Controlador "limpio" que solo "pasa la pelota"

using Microsoft.AspNetCore.Mvc;
using System; // Para Exception
using System.Threading.Tasks; // Para Task
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

        // --- Endpoint de Registro (¡LIMPIO!) ---
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

        // --- Endpoint de Login (¡LIMPIO!) ---
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
    }
}