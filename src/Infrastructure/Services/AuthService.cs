using Microsoft.AspNetCore.Identity;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services
{
    public class AuthService(VisioAnalyticaDbContext context,
                       UserManager<User> userManager,
                       ITokenService tokenService) : IAuthService
    {
        private readonly VisioAnalyticaDbContext _context = context;
        private readonly UserManager<User> _userManager = userManager;
        private readonly ITokenService _tokenService = tokenService;

        public async Task<UserDto> LoginAsync(LoginDto loginDto)
        {
            // No usamos .ToLower() aquí. UserManager se encarga
            // de la normalización (buscará contra NormalizedEmail).
            var user = await _userManager.FindByEmailAsync(loginDto.Email) ?? throw new UnauthorizedAccessException("Email o contraseña inválidos.");
            
            // Verificar que el usuario esté activo
            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("Tu cuenta ha sido desactivada. Contacta al administrador.");
            }
            
            var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, loginDto.Password);
            if (!isPasswordCorrect)
            {
                throw new UnauthorizedAccessException("Email o contraseña inválidos.");
            }

            // Obtener los roles del usuario
            var roles = await _userManager.GetRolesAsync(user);

            // Le decimos al compilador que confiamos en que 'user.Email'
            // y 'user.FirstName' no son nulos en este punto, usando el '!'
            return new UserDto(user.Email!, user.FirstName!, _tokenService.CreateToken(user, roles));
        }

        public async Task<UserDto> RegisterAsync(RegisterDto registerDto)
        {
            // ¡No usamos .AnyAsync(u => u.Email...)!
            // Usamos el método optimizado del UserManager, que usa el índice.
            var existingUser = await _userManager.FindByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                throw new ArgumentException("El correo electrónico ya está en uso.");
            }

            // ¡Toda la lógica de la transacción vive aquí ahora!
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var organization = new Organization
                {
                    Name = registerDto.OrganizationName
                };
                _context.Organizations.Add(organization);
                await _context.SaveChangesAsync();

                var user = new User
                {
                    FirstName = registerDto.FirstName,
                    LastName = registerDto.LastName,
                    Email = registerDto.Email.ToLower(), // (Aquí sí podemos usar ToLower para guardar)
                    UserName = registerDto.Email.ToLower(),
                    OrganizationId = organization.Id
                };

                var result = await _userManager.CreateAsync(user, registerDto.Password);

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException($"Falló la creación del usuario: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                await transaction.CommitAsync();

                // Obtener roles del usuario (puede estar vacío si no se asignaron roles)
                var roles = await _userManager.GetRolesAsync(user);

                return new UserDto(user.Email, user.FirstName, _tokenService.CreateToken(user, roles));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}