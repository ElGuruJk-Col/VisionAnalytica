using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato (Interfaz) para un servicio que genera
    /// tokens de autenticación (JWT).
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
    }
}

