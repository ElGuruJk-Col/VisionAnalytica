using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para el servicio de gestión de usuarios.
    /// Permite crear usuarios, asignar roles, y gestionar sus propiedades.
    /// </summary>
    public interface IUserManagementService
    {
        /// <summary>
        /// Crea un nuevo usuario con contraseña provisional.
        /// El usuario deberá cambiar su contraseña en el primer inicio de sesión.
        /// </summary>
        /// <param name="request">DTO con los datos del usuario a crear</param>
        /// <param name="createdByUserId">ID del usuario que está creando este usuario (SuperAdmin o Admin)</param>
        /// <returns>DTO con los datos del usuario creado, incluyendo la contraseña provisional</returns>
        Task<CreateUserResponseDto> CreateUserAsync(CreateUserRequestDto request, Guid createdByUserId);

        /// <summary>
        /// Asigna un rol a un usuario.
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="roleName">Nombre del rol a asignar</param>
        /// <returns>True si se asignó correctamente, False en caso contrario</returns>
        Task<bool> AssignRoleAsync(Guid userId, string roleName);

        /// <summary>
        /// Remueve un rol de un usuario.
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="roleName">Nombre del rol a remover</param>
        /// <returns>True si se removió correctamente, False en caso contrario</returns>
        Task<bool> RemoveRoleAsync(Guid userId, string roleName);

        /// <summary>
        /// Obtiene todos los roles de un usuario.
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <returns>Lista de nombres de roles</returns>
        Task<IList<string>> GetUserRolesAsync(Guid userId);

        /// <summary>
        /// Activa o desactiva un usuario.
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="isActive">True para activar, False para desactivar</param>
        /// <returns>True si se actualizó correctamente</returns>
        Task<bool> SetUserActiveStatusAsync(Guid userId, bool isActive);

        /// <summary>
        /// Genera una contraseña provisional aleatoria.
        /// </summary>
        /// <param name="length">Longitud de la contraseña (default: 12)</param>
        /// <returns>Contraseña generada</returns>
        string GenerateTemporaryPassword(int length = 12);

        /// <summary>
        /// Obtiene un usuario por su ID.
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <returns>Usuario encontrado o null</returns>
        Task<User?> GetUserByIdAsync(Guid userId);

        /// <summary>
        /// Obtiene todos los usuarios de una organización.
        /// </summary>
        /// <param name="organizationId">ID de la organización</param>
        /// <param name="includeInactive">Si incluir usuarios inactivos</param>
        /// <returns>Lista de usuarios</returns>
        Task<IList<User>> GetUsersByOrganizationAsync(Guid organizationId, bool includeInactive = false);

        /// <summary>
        /// Obtiene todos los usuarios con un rol específico en una organización.
        /// </summary>
        /// <param name="organizationId">ID de la organización</param>
        /// <param name="roleName">Nombre del rol</param>
        /// <returns>Lista de usuarios</returns>
        Task<IList<User>> GetUsersByRoleAsync(Guid organizationId, string roleName);
    }
}

