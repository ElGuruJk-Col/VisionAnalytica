using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Contrato para el servicio de gestión de empresas afiliadas.
    /// Permite crear, actualizar y gestionar empresas afiliadas y sus asignaciones.
    /// </summary>
    public interface IAffiliatedCompanyService
    {
        /// <summary>
        /// Crea una nueva empresa afiliada.
        /// </summary>
        /// <param name="request">DTO con los datos de la empresa</param>
        /// <param name="createdByUserId">ID del usuario que está creando la empresa (Admin)</param>
        /// <returns>DTO con los datos de la empresa creada</returns>
        Task<AffiliatedCompanyDto> CreateAsync(CreateAffiliatedCompanyDto request, Guid createdByUserId);

        /// <summary>
        /// Actualiza una empresa afiliada existente.
        /// </summary>
        /// <param name="companyId">ID de la empresa</param>
        /// <param name="request">DTO con los datos actualizados</param>
        /// <returns>DTO con los datos actualizados</returns>
        Task<AffiliatedCompanyDto> UpdateAsync(Guid companyId, UpdateAffiliatedCompanyDto request);

        /// <summary>
        /// Obtiene una empresa afiliada por su ID.
        /// </summary>
        /// <param name="companyId">ID de la empresa</param>
        /// <param name="organizationId">ID de la organización (para validar permisos)</param>
        /// <returns>DTO de la empresa o null si no existe</returns>
        Task<AffiliatedCompanyDto?> GetByIdAsync(Guid companyId, Guid organizationId);

        /// <summary>
        /// Obtiene todas las empresas afiliadas de una organización.
        /// </summary>
        /// <param name="organizationId">ID de la organización</param>
        /// <param name="includeInactive">Si incluir empresas inactivas</param>
        /// <returns>Lista de empresas afiliadas</returns>
        Task<IList<AffiliatedCompanyDto>> GetByOrganizationAsync(Guid organizationId, bool includeInactive = false);

        /// <summary>
        /// Asigna un inspector a una empresa afiliada.
        /// </summary>
        /// <param name="companyId">ID de la empresa</param>
        /// <param name="inspectorId">ID del inspector</param>
        /// <param name="organizationId">ID de la organización (para validar permisos)</param>
        /// <returns>True si se asignó correctamente</returns>
        Task<bool> AssignInspectorAsync(Guid companyId, Guid inspectorId, Guid organizationId);

        /// <summary>
        /// Remueve un inspector de una empresa afiliada.
        /// </summary>
        /// <param name="companyId">ID de la empresa</param>
        /// <param name="inspectorId">ID del inspector</param>
        /// <param name="organizationId">ID de la organización (para validar permisos)</param>
        /// <returns>True si se removió correctamente</returns>
        Task<bool> RemoveInspectorAsync(Guid companyId, Guid inspectorId, Guid organizationId);

        /// <summary>
        /// Obtiene todas las empresas afiliadas asignadas a un inspector.
        /// </summary>
        /// <param name="inspectorId">ID del inspector</param>
        /// <param name="includeInactive">Si incluir empresas inactivas</param>
        /// <returns>Lista de empresas afiliadas</returns>
        Task<IList<AffiliatedCompanyDto>> GetByInspectorAsync(Guid inspectorId, bool includeInactive = false);

        /// <summary>
        /// Obtiene todos los inspectores asignados a una empresa afiliada.
        /// </summary>
        /// <param name="companyId">ID de la empresa</param>
        /// <param name="organizationId">ID de la organización (para validar permisos)</param>
        /// <returns>Lista de usuarios inspectores</returns>
        Task<IList<User>> GetAssignedInspectorsAsync(Guid companyId, Guid organizationId);

        /// <summary>
        /// Activa o desactiva una empresa afiliada.
        /// </summary>
        /// <param name="companyId">ID de la empresa</param>
        /// <param name="isActive">True para activar, False para desactivar</param>
        /// <param name="organizationId">ID de la organización (para validar permisos)</param>
        /// <returns>True si se actualizó correctamente</returns>
        Task<bool> SetActiveStatusAsync(Guid companyId, bool isActive, Guid organizationId);

        /// <summary>
        /// Elimina una empresa afiliada (soft delete marcándola como inactiva).
        /// </summary>
        /// <param name="companyId">ID de la empresa</param>
        /// <param name="organizationId">ID de la organización (para validar permisos)</param>
        /// <returns>True si se eliminó correctamente</returns>
        Task<bool> DeleteAsync(Guid companyId, Guid organizationId);
    }
}

