using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación del servicio de gestión de empresas afiliadas.
    /// </summary>
    public class AffiliatedCompanyService : IAffiliatedCompanyService
    {
        private readonly VisioAnalyticaDbContext _context;
        private readonly UserManager<User> _userManager;

        public AffiliatedCompanyService(
            VisioAnalyticaDbContext context,
            UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<AffiliatedCompanyDto> CreateAsync(CreateAffiliatedCompanyDto request, Guid createdByUserId)
        {
            // Verificar que la organización existe
            var organization = await _context.Organizations.FindAsync(request.OrganizationId);
            if (organization == null)
            {
                throw new ArgumentException("La organización especificada no existe.");
            }

            // Verificar que no exista otra empresa con el mismo nombre en la misma organización
            var existingCompany = await _context.AffiliatedCompanies
                .FirstOrDefaultAsync(ac => ac.Name == request.Name && ac.OrganizationId == request.OrganizationId);

            if (existingCompany != null)
            {
                throw new ArgumentException("Ya existe una empresa afiliada con ese nombre en esta organización.");
            }

            var company = new AffiliatedCompany
            {
                Name = request.Name,
                TaxId = request.TaxId,
                Address = request.Address,
                Phone = request.Phone,
                Email = request.Email,
                OrganizationId = request.OrganizationId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdByUserId
            };

            _context.AffiliatedCompanies.Add(company);
            await _context.SaveChangesAsync();

            return await MapToDtoAsync(company);
        }

        public async Task<AffiliatedCompanyDto> UpdateAsync(Guid companyId, UpdateAffiliatedCompanyDto request)
        {
            var company = await _context.AffiliatedCompanies.FindAsync(companyId);
            if (company == null)
            {
                throw new ArgumentException("La empresa afiliada no existe.");
            }

            // Actualizar solo los campos proporcionados
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                // Verificar que el nuevo nombre no esté en uso por otra empresa
                var existingCompany = await _context.AffiliatedCompanies
                    .FirstOrDefaultAsync(ac => ac.Name == request.Name && 
                                               ac.OrganizationId == company.OrganizationId && 
                                               ac.Id != companyId);

                if (existingCompany != null)
                {
                    throw new ArgumentException("Ya existe otra empresa afiliada con ese nombre en esta organización.");
                }

                company.Name = request.Name;
            }

            if (request.TaxId != null) company.TaxId = request.TaxId;
            if (request.Address != null) company.Address = request.Address;
            if (request.Phone != null) company.Phone = request.Phone;
            if (request.Email != null) company.Email = request.Email;
            if (request.IsActive.HasValue) company.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();

            return await MapToDtoAsync(company);
        }

        public async Task<AffiliatedCompanyDto?> GetByIdAsync(Guid companyId, Guid organizationId)
        {
            var company = await _context.AffiliatedCompanies
                .Include(ac => ac.Organization)
                .FirstOrDefaultAsync(ac => ac.Id == companyId && ac.OrganizationId == organizationId);

            if (company == null)
            {
                return null;
            }

            return await MapToDtoAsync(company);
        }

        public async Task<IList<AffiliatedCompanyDto>> GetByOrganizationAsync(Guid organizationId, bool includeInactive = false)
        {
            var query = _context.AffiliatedCompanies
                .Include(ac => ac.Organization)
                .Where(ac => ac.OrganizationId == organizationId);

            if (!includeInactive)
            {
                query = query.Where(ac => ac.IsActive);
            }

            var companies = await query.ToListAsync();
            var dtos = new List<AffiliatedCompanyDto>();

            foreach (var company in companies)
            {
                dtos.Add(await MapToDtoAsync(company));
            }

            return dtos;
        }

        public async Task<bool> AssignInspectorAsync(Guid companyId, Guid inspectorId, Guid organizationId)
        {
            // Verificar que la empresa existe y pertenece a la organización
            var company = await _context.AffiliatedCompanies
                .Include(ac => ac.AssignedInspectors)
                .FirstOrDefaultAsync(ac => ac.Id == companyId && ac.OrganizationId == organizationId);

            if (company == null)
            {
                throw new ArgumentException("La empresa afiliada no existe o no pertenece a esta organización.");
            }

            // Verificar que el inspector existe y pertenece a la organización
            var inspector = await _userManager.FindByIdAsync(inspectorId.ToString());
            if (inspector == null || inspector.OrganizationId != organizationId)
            {
                throw new ArgumentException("El inspector no existe o no pertenece a esta organización.");
            }

            // Verificar que el usuario es un inspector
            var isInspector = await _userManager.IsInRoleAsync(inspector, Core.Constants.Roles.Inspector);
            if (!isInspector)
            {
                throw new ArgumentException("El usuario especificado no es un inspector.");
            }

            // Verificar que no esté ya asignado
            if (company.AssignedInspectors.Any(i => i.Id == inspectorId))
            {
                return true; // Ya está asignado, consideramos éxito
            }

            company.AssignedInspectors.Add(inspector);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> RemoveInspectorAsync(Guid companyId, Guid inspectorId, Guid organizationId)
        {
            var company = await _context.AffiliatedCompanies
                .Include(ac => ac.AssignedInspectors)
                .FirstOrDefaultAsync(ac => ac.Id == companyId && ac.OrganizationId == organizationId);

            if (company == null)
            {
                throw new ArgumentException("La empresa afiliada no existe o no pertenece a esta organización.");
            }

            var inspector = company.AssignedInspectors.FirstOrDefault(i => i.Id == inspectorId);
            if (inspector == null)
            {
                return true; // No está asignado, consideramos éxito
            }

            company.AssignedInspectors.Remove(inspector);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IList<AffiliatedCompanyDto>> GetByInspectorAsync(Guid inspectorId, bool includeInactive = false)
        {
            var inspector = await _userManager.FindByIdAsync(inspectorId.ToString());
            if (inspector == null)
            {
                return new List<AffiliatedCompanyDto>();
            }

            var query = _context.AffiliatedCompanies
                .Include(ac => ac.Organization)
                .Include(ac => ac.AssignedInspectors)
                .Where(ac => ac.AssignedInspectors.Any(i => i.Id == inspectorId));

            if (!includeInactive)
            {
                query = query.Where(ac => ac.IsActive);
            }

            var companies = await query.ToListAsync();
            var dtos = new List<AffiliatedCompanyDto>();

            foreach (var company in companies)
            {
                dtos.Add(await MapToDtoAsync(company));
            }

            return dtos;
        }

        public async Task<IList<User>> GetAssignedInspectorsAsync(Guid companyId, Guid organizationId)
        {
            var company = await _context.AffiliatedCompanies
                .Include(ac => ac.AssignedInspectors)
                .FirstOrDefaultAsync(ac => ac.Id == companyId && ac.OrganizationId == organizationId);

            if (company == null)
            {
                throw new ArgumentException("La empresa afiliada no existe o no pertenece a esta organización.");
            }

            return company.AssignedInspectors.ToList();
        }

        public async Task<bool> SetActiveStatusAsync(Guid companyId, bool isActive, Guid organizationId)
        {
            var company = await _context.AffiliatedCompanies
                .FirstOrDefaultAsync(ac => ac.Id == companyId && ac.OrganizationId == organizationId);

            if (company == null)
            {
                return false;
            }

            company.IsActive = isActive;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> DeleteAsync(Guid companyId, Guid organizationId)
        {
            // Soft delete: solo marcamos como inactiva
            return await SetActiveStatusAsync(companyId, false, organizationId);
        }

        /// <summary>
        /// Mapea una entidad AffiliatedCompany a su DTO.
        /// </summary>
        private async Task<AffiliatedCompanyDto> MapToDtoAsync(AffiliatedCompany company)
        {
            // Cargar la organización si no está cargada
            if (company.Organization == null)
            {
                await _context.Entry(company)
                    .Reference(c => c.Organization)
                    .LoadAsync();
            }

            // Cargar inspectores asignados si no están cargados
            if (company.AssignedInspectors == null || !company.AssignedInspectors.Any())
            {
                await _context.Entry(company)
                    .Collection(c => c.AssignedInspectors)
                    .LoadAsync();
            }

            // Contar inspecciones
            var inspectionsCount = await _context.Inspections
                .CountAsync(i => i.AffiliatedCompanyId == company.Id);

            return new AffiliatedCompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                TaxId = company.TaxId,
                Address = company.Address,
                Phone = company.Phone,
                Email = company.Email,
                OrganizationId = company.OrganizationId,
                OrganizationName = company.Organization?.Name ?? string.Empty,
                IsActive = company.IsActive,
                CreatedAt = company.CreatedAt,
                AssignedInspectorsCount = company.AssignedInspectors?.Count ?? 0,
                InspectionsCount = inspectionsCount
            };
        }
    }
}

