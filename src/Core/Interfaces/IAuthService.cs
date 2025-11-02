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
    }
}