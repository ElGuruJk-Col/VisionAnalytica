using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services
{
    public class AuthService(VisioAnalyticaDbContext context,
                       UserManager<User> userManager,
                       ITokenService tokenService,
                       IEmailService? emailService = null,
                       IConfiguration? configuration = null) : IAuthService
    {
        private readonly VisioAnalyticaDbContext _context = context;
        private readonly UserManager<User> _userManager = userManager;
        private readonly ITokenService _tokenService = tokenService;
        private readonly IEmailService? _emailService = emailService;
        private readonly IConfiguration? _configuration = configuration;

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

            // Verificar si la cuenta está bloqueada
            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                var minutesRemaining = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
                throw new UnauthorizedAccessException($"Tu cuenta está bloqueada temporalmente. Intenta nuevamente en {minutesRemaining} minuto(s). Si olvidaste tu contraseña, solicita una nueva contraseña.");
            }

            // Si el bloqueo expiró, resetear el estado
            if (user.LockedUntil.HasValue && user.LockedUntil.Value <= DateTime.UtcNow)
            {
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                await _userManager.UpdateAsync(user);
            }
            
            var isPasswordCorrect = await _userManager.CheckPasswordAsync(user, loginDto.Password);
            if (!isPasswordCorrect)
            {
                // Incrementar intentos fallidos
                user.FailedLoginAttempts++;
                
                // Si alcanza 3 intentos fallidos, bloquear la cuenta
                if (user.FailedLoginAttempts >= 3)
                {
                    // Bloquear por 30 minutos
                    user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                    await _userManager.UpdateAsync(user);
                    
                    // Enviar notificación de bloqueo por email
                    if (_emailService != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _emailService.SendAccountLockedEmailAsync(user.Email!, user.FirstName ?? "Usuario");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[WARNING] No se pudo enviar email de bloqueo a {user.Email}: {ex.Message}");
                            }
                        });
                    }
                    
                    throw new UnauthorizedAccessException("Tu cuenta ha sido bloqueada temporalmente debido a múltiples intentos fallidos. Se ha enviado un email con instrucciones. El bloqueo durará 30 minutos.");
                }
                else
                {
                    await _userManager.UpdateAsync(user);
                    var remainingAttempts = 3 - user.FailedLoginAttempts;
                    throw new UnauthorizedAccessException($"Email o contraseña inválidos. Te quedan {remainingAttempts} intento(s) antes de que tu cuenta se bloquee.");
                }
            }

            // Login exitoso: resetear intentos fallidos y desbloquear si estaba bloqueado
            if (user.FailedLoginAttempts > 0 || user.LockedUntil.HasValue)
            {
                user.FailedLoginAttempts = 0;
                user.LockedUntil = null;
                await _userManager.UpdateAsync(user);
            }

            // Obtener los roles del usuario
            var roles = await _userManager.GetRolesAsync(user);

            // Generar access token
            var accessToken = _tokenService.CreateToken(user, roles);
            
            // Generar y guardar refresh token
            var refreshToken = await GenerateAndSaveRefreshTokenAsync(user, null);

            // Le decimos al compilador que confiamos en que 'user.Email'
            // y 'user.FirstName' no son nulos en este punto, usando el '!'
            return new UserDto(user.Email!, user.FirstName!, accessToken, refreshToken);
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

                // Generar access token
                var accessToken = _tokenService.CreateToken(user, roles);
                
                // Generar y guardar refresh token
                var refreshToken = await GenerateAndSaveRefreshTokenAsync(user, null);

                return new UserDto(user.Email, user.FirstName, accessToken, refreshToken);
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

            IdentityResult result;
            
            // Si CurrentPassword es null o vacío, significa que viene de contraseña temporal (MustChangePassword = true)
            if (string.IsNullOrWhiteSpace(changePasswordDto.CurrentPassword))
            {
                if (!user.MustChangePassword)
                {
                    throw new InvalidOperationException("Se requiere la contraseña actual para cambiar la contraseña.");
                }
                
                // Cambiar contraseña sin verificar la actual (contraseña temporal)
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                result = await _userManager.ResetPasswordAsync(user, resetToken, changePasswordDto.NewPassword);
            }
            else
            {
                // Cambio de contraseña normal (requiere la contraseña actual)
                result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
            }
            
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Error al cambiar la contraseña: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Actualizar fecha de cambio de contraseña y marcar que ya no debe cambiarla
            user.PasswordChangedAt = DateTime.UtcNow;
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            // Revocar todos los refresh tokens del usuario por seguridad
            await RevokeAllRefreshTokensForUserAsync(user.Id, null, "Password changed");

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

            // Desbloquear la cuenta si estaba bloqueada (al solicitar nueva contraseña)
            if (user.LockedUntil.HasValue)
            {
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                await _userManager.UpdateAsync(user);
            }

            // Generar una contraseña temporal segura
            var temporaryPassword = GenerateTemporaryPassword();
            
            // Generar token de recuperación para resetear la contraseña
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            
            // Resetear la contraseña del usuario con la contraseña temporal
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, temporaryPassword);
            if (!resetResult.Succeeded)
            {
                // Si falla el reset, loguear el error con detalles para debugging
                var errors = string.Join(", ", resetResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
                Console.WriteLine($"[WARNING] No se pudo resetear la contraseña para {user.Email}. Errores: {errors}");
                // Lanzar excepción para que el controlador pueda manejarla apropiadamente
                throw new InvalidOperationException($"No se pudo generar la contraseña temporal. Errores: {errors}");
            }

            // Marcar que el usuario debe cambiar su contraseña en el próximo login
            user.MustChangePassword = true;
            user.PasswordChangedAt = null; // Resetear la fecha de cambio
            await _userManager.UpdateAsync(user);

            // Enviar email con la contraseña temporal
            if (_emailService != null)
            {
                var emailSent = await _emailService.SendPasswordResetEmailAsync(user.Email!, temporaryPassword, user.FirstName ?? "Usuario");
                if (!emailSent)
                {
                    // Log el error pero no fallar la operación (por seguridad)
                    Console.WriteLine($"[WARNING] No se pudo enviar el email de recuperación a {user.Email}");
                }
            }
            else
            {
                // En desarrollo, si no hay servicio de email configurado, loguear la contraseña temporal
                Console.WriteLine($"[DEV ONLY] Contraseña temporal para {user.Email}: {temporaryPassword}");
            }

            return true;
        }

        /// <summary>
        /// Genera una contraseña temporal segura que cumple con la política de contraseñas:
        /// - Mínimo 8 caracteres
        /// - Requiere dígitos
        /// - Requiere minúsculas
        /// - Requiere mayúsculas
        /// - Requiere caracteres no alfanuméricos
        /// </summary>
        private static string GenerateTemporaryPassword()
        {
            // Generar una contraseña de 12 caracteres con mayúsculas, minúsculas, números y caracteres especiales
            const string upperCase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lowerCase = "abcdefghijkmnpqrstuvwxyz";
            const string numbers = "23456789";
            const string specialChars = "!@#$%&*";
            const string allChars = upperCase + lowerCase + numbers + specialChars;
            
            var random = new Random();
            var password = new char[12];
            
            // Asegurar al menos un carácter de cada tipo requerido
            password[0] = upperCase[random.Next(upperCase.Length)];
            password[1] = lowerCase[random.Next(lowerCase.Length)];
            password[2] = numbers[random.Next(numbers.Length)];
            password[3] = specialChars[random.Next(specialChars.Length)];
            
            // Llenar el resto con caracteres aleatorios
            for (int i = 4; i < password.Length; i++)
            {
                password[i] = allChars[random.Next(allChars.Length)];
            }
            
            // Mezclar los caracteres
            for (int i = password.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }
            
            return new string(password);
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

            // Revocar todos los refresh tokens del usuario por seguridad
            await RevokeAllRefreshTokensForUserAsync(user.Id, null, "Password changed");

            return true;
        }

        /// <summary>
        /// Genera un refresh token y lo guarda en la base de datos.
        /// </summary>
        private async Task<string> GenerateAndSaveRefreshTokenAsync(User user, string? ipAddress)
        {
            // Generar token único
            var token = _tokenService.GenerateRefreshToken();
            
            // Obtener tiempo de expiración desde configuración (por defecto: 7 días)
            var expirationDays = _configuration?.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7) ?? 7;
            var expiresAt = DateTime.UtcNow.AddDays(expirationDays);
            
            // Crear entidad RefreshToken
            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = token,
                UserId = user.Id,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
            
            // Guardar en base de datos
            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            
            return token;
        }

        /// <summary>
        /// Revoca un refresh token (lo marca como revocado).
        /// </summary>
        private async Task RevokeRefreshTokenAsync(string token, string? ipAddress, string? reason = null)
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token);

            if (refreshToken != null && refreshToken.IsActive)
            {
                refreshToken.RevokedAt = DateTime.UtcNow;
                refreshToken.RevokedByIp = ipAddress;
                refreshToken.ReasonRevoked = reason;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Revoca todos los refresh tokens de un usuario (útil al cambiar contraseña o hacer logout).
        /// </summary>
        private async Task RevokeAllRefreshTokensForUserAsync(Guid userId, string? ipAddress, string? reason = null)
        {
            var activeTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.IsActive)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = reason;
            }

            if (activeTokens.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Renueva un access token usando un refresh token válido.
        /// </summary>
        public async Task<RefreshTokenResponseDto?> RefreshTokenAsync(string refreshToken)
        {
            // Validar el refresh token
            var userId = await _tokenService.ValidateRefreshTokenAsync(refreshToken);
            if (!userId.HasValue)
            {
                return null; // Token inválido, expirado o revocado
            }

            // Obtener el usuario
            var user = await _userManager.FindByIdAsync(userId.Value.ToString());
            if (user == null || !user.IsActive)
            {
                return null;
            }

            // Revocar el refresh token usado (rotación de tokens)
            await RevokeRefreshTokenAsync(refreshToken, null, "Used for token refresh");

            // Obtener roles del usuario
            var roles = await _userManager.GetRolesAsync(user);

            // Generar nuevo access token
            var newAccessToken = _tokenService.CreateToken(user, roles);

            // Generar nuevo refresh token
            var newRefreshToken = await GenerateAndSaveRefreshTokenAsync(user, null);

            return new RefreshTokenResponseDto(newAccessToken, newRefreshToken);
        }

        /// <summary>
        /// Revoca un refresh token específico del usuario autenticado.
        /// </summary>
        public async Task<bool> RevokeTokenAsync(Guid userId, string refreshToken, string? ipAddress = null)
        {
            // Buscar el token y verificar que pertenezca al usuario
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId);

            if (token == null)
            {
                return false; // Token no existe o no pertenece al usuario
            }

            // Solo revocar si está activo
            if (token.IsActive)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIp = ipAddress;
                token.ReasonRevoked = "Revoked by user";
                await _context.SaveChangesAsync();
                return true;
            }

            return false; // Token ya estaba revocado o expirado
        }

        /// <summary>
        /// Obtiene todos los refresh tokens activos del usuario.
        /// </summary>
        public async Task<List<RefreshTokenInfoDto>> GetMyRefreshTokensAsync(Guid userId)
        {
            var tokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .OrderByDescending(rt => rt.CreatedAt)
                .Select(rt => new RefreshTokenInfoDto(
                    rt.Id,
                    rt.CreatedAt,
                    rt.ExpiresAt,
                    rt.RevokedAt,
                    rt.CreatedByIp,
                    rt.IsActive
                ))
                .ToListAsync();

            return tokens;
        }
    }
}