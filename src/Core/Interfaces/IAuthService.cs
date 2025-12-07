// En: src/Core/Interfaces/IAuthService.cs
// (¡NUEVO ARCHIVO!)
// Tarea 30.B: El "Contrato" para el servicio de autenticación

using System.Threading.Tasks;
using VisioAnalytica.Core.Models.Dtos; // <-- ¡Referencia Pura!

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para el servicio de autenticación.
    /// Abstrae la lógica de negocio de registro y login.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registra una nueva organización y su primer usuario.
        /// </summary>
        /// <param name="registerDto">Datos de registro</param>
        /// <returns>Un DTO de usuario con el token JWT</returns>
        Task<UserDto> RegisterAsync(RegisterDto registerDto);

        /// <summary>
        /// Loguea a un usuario existente.
        /// </summary>
        /// <param name="loginDto">Datos de login</param>
        /// <returns>Un DTO de usuario con el token JWT</returns>
        Task<UserDto> LoginAsync(LoginDto loginDto);

        /// <summary>
        /// Cambia la contraseña de un usuario autenticado.
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="changePasswordDto">Datos de cambio de contraseña</param>
        /// <returns>True si se cambió correctamente</returns>
        Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto);

        /// <summary>
        /// Solicita la recuperación de contraseña enviando un token por email.
        /// </summary>
        /// <param name="forgotPasswordDto">Email del usuario</param>
        /// <returns>True si se envió el email correctamente</returns>
        Task<bool> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);

        /// <summary>
        /// Restablece la contraseña usando un token de recuperación.
        /// </summary>
        /// <param name="resetPasswordDto">Datos de restablecimiento</param>
        /// <returns>True si se restableció correctamente</returns>
        Task<bool> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);

        /// <summary>
        /// Renueva un access token usando un refresh token válido.
        /// </summary>
        /// <param name="refreshToken">El refresh token a usar para renovar</param>
        /// <returns>Nuevo access token y refresh token, o null si el refresh token es inválido</returns>
        Task<RefreshTokenResponseDto?> RefreshTokenAsync(string refreshToken);

        /// <summary>
        /// Revoca un refresh token específico del usuario autenticado.
        /// </summary>
        /// <param name="userId">ID del usuario propietario del token</param>
        /// <param name="refreshToken">El refresh token a revocar</param>
        /// <param name="ipAddress">Dirección IP desde la cual se revoca (opcional, para auditoría)</param>
        /// <returns>True si se revocó correctamente, false si el token no existe o no pertenece al usuario</returns>
        Task<bool> RevokeTokenAsync(Guid userId, string refreshToken, string? ipAddress = null);

        /// <summary>
        /// Obtiene todos los refresh tokens activos del usuario.
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <returns>Lista de refresh tokens activos del usuario</returns>
        Task<List<RefreshTokenInfoDto>> GetMyRefreshTokensAsync(Guid userId);
    }
}