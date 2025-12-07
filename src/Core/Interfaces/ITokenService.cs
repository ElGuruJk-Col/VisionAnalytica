using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato (Interfaz) para un servicio que genera
    /// tokens de autenticación (JWT) y refresh tokens.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Genera un string de token JWT para un usuario específico.
        /// </summary>
        /// <param name="user">El usuario (de Identity) para el cual se genera el token.</param>
        /// <param name="roles">Lista de roles del usuario. Si es null, no se incluyen roles en el token.</param>
        /// <returns>Un string que representa el token JWT.</returns>
        string CreateToken(User user, IList<string>? roles = null);

        /// <summary>
        /// Genera un refresh token único para un usuario.
        /// </summary>
        /// <param name="user">El usuario para el cual se genera el refresh token.</param>
        /// <param name="ipAddress">Dirección IP desde la cual se solicita el token (opcional, para auditoría).</param>
        /// <returns>Un string que representa el refresh token.</returns>
        string GenerateRefreshToken();

        /// <summary>
        /// Valida un refresh token y devuelve el usuario asociado si es válido.
        /// </summary>
        /// <param name="token">El refresh token a validar.</param>
        /// <returns>El ID del usuario si el token es válido, null en caso contrario.</returns>
        Task<Guid?> ValidateRefreshTokenAsync(string token);
    }
}

