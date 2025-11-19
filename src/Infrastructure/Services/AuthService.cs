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

        public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                throw new ArgumentException("Usuario no encontrado.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("La cuenta de usuario está inactiva.");
            }

            var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Error al cambiar la contraseña: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Actualizar fecha de cambio de contraseña y marcar que ya no debe cambiarla
            user.PasswordChangedAt = DateTime.UtcNow;
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            return true;
        }

        public async Task<bool> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
            if (user == null)
            {
                // Por seguridad, no revelamos si el email existe o no
                return true;
            }

            if (!user.IsActive)
            {
                // Por seguridad, no revelamos si la cuenta está inactiva
                return true;
            }

            // Generar token de recuperación de contraseña
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            
            // TODO: Enviar email con el token
            // Por ahora, solo logueamos el token (en producción, esto debe enviarse por email)
            // En desarrollo, podrías exponer esto temporalmente, pero NUNCA en producción
            Console.WriteLine($"[DEV ONLY] Token de recuperación para {user.Email}: {token}");

            return true;
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
            if (user == null)
            {
                throw new ArgumentException("Usuario no encontrado.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException("La cuenta de usuario está inactiva.");
            }

            var result = await _userManager.ResetPasswordAsync(user, resetPasswordDto.Token, resetPasswordDto.NewPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Error al restablecer la contraseña: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Actualizar fecha de cambio de contraseña y marcar que ya no debe cambiarla
            user.PasswordChangedAt = DateTime.UtcNow;
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            return true;
        }
    }
}