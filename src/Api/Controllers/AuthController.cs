using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        // --- Inyección de Dependencias ---
        // Aquí "pedimos" todas las herramientas que registramos en Program.cs
        private readonly UserManager<User> _userManager;
        private readonly VisioAnalyticaDbContext _context;
        private readonly ITokenService _tokenService;

        public AuthController(
            UserManager<User> userManager,
            VisioAnalyticaDbContext context,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _context = context;
            _tokenService = tokenService;
        }

        // --- ENDPOINT DE REGISTRO ---
        // URL: POST /api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            // 1. Validar que el email no exista
            var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return BadRequest("El correo electrónico ya está en uso.");
            }

            // 2. Crear la Organización (Empresa)
            // Usamos una "Transacción" para asegurar que o se crea
            // la empresa Y el usuario, o no se crea nada (Atomocidad)
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newOrganization = new Organization
                {
                    Name = registerDto.OrganizationName
                };
                await _context.Organizations.AddAsync(newOrganization);
                await _context.SaveChangesAsync(); // Guardamos para obtener el ID

                // 3. Crear el Usuario (Inspector)
                var newUser = new User
                {
                    FirstName = registerDto.FirstName,
                    LastName = registerDto.LastName,
                    Email = registerDto.Email,
                    UserName = registerDto.Email, // Usamos el email como UserName
                    OrganizationId = newOrganization.Id // ¡Lo vinculamos a la empresa!
                };

                var result = await _userManager.CreateAsync(newUser, registerDto.Password);

                if (!result.Succeeded)
                {
                    // Si falla (ej. password débil), revertimos todo
                    await transaction.RollbackAsync();
                    return BadRequest(result.Errors);
                }

                // 4. Si todo salió bien, "compramos" la transacción
                await transaction.CommitAsync();

                // 5. Creamos la "llave" (Token) y la devolvemos
                return Ok(new AuthResponseDto
                {
                    Email = newUser.Email,
                    FirstName = newUser.FirstName,
                    Token = _tokenService.CreateToken(newUser)
                });
            }
            catch (Exception)
            {
                // Si algo falló (ej. BBDD se cayó), revertimos
                await transaction.RollbackAsync();
                return StatusCode(500, "Error interno del servidor al crear la organización.");
            }
        }

        // --- ENDPOINT DE LOGIN ---
        // URL: POST /api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            // 1. Buscar al usuario por su email
            var user = await _userManager.Users
                                 .Include(u => u.Organization) // Traemos los datos de su empresa
                                 .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
            {
                return Unauthorized("Email o contraseña incorrectos."); // ¡No dar pistas!
            }

            // 2. Comprobar su contraseña
            var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, loginDto.Password);

            if (!isPasswordCorrect)
            {
                return Unauthorized("Email o contraseña incorrectos.");
            }

            // 3. Si todo es correcto, crear la "llave" y devolverla
            return Ok(new AuthResponseDto
            {
                Email = user.Email!,
                FirstName = user.FirstName,
                Token = _tokenService.CreateToken(user)
            });
        }
    }

    // --- DTOs (Data Transfer Objects) ---
    // Clases "contenedoras" que definen los JSON que esperamos
    // recibir (Request) y devolver (Response).
    // Las definimos aquí mismo para simplificar este paso.

    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;

        [Required]
        public string FirstName { get; set; } = null!;

        [Required]
        public string LastName { get; set; } = null!;

        [Required]
        public string OrganizationName { get; set; } = null!;
    }

    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;
    }

    public class AuthResponseDto
    {
        public string Email { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string Token { get; set; } = null!;
    }
}
