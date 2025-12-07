using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Infrastructure.Data
{
    /// <summary>
    /// Implementación de IAnalysisRepository. 
    /// Maneja la interacción directa con Entity Framework Core.
    /// </summary>
    public class AnalysisRepository : IAnalysisRepository
    {
        // Campos de solo lectura inyectados por el constructor
        private readonly VisioAnalyticaDbContext _context;
        private readonly ILogger<AnalysisRepository> _logger;

        public AnalysisRepository(VisioAnalyticaDbContext context, ILogger<AnalysisRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("AnalysisRepository inicializado.");
        }

        // --- MÉTODOS DE ESCRITURA (Save) ---

        public async Task<Inspection> SaveInspectionAsync(Inspection inspection)
        {
            // 1. Añadimos el objeto raíz (Inspection).
            //    Gracias a Entity Framework, todos los objetos anidados (Findings)
            //    que estén dentro de la colección 'Findings' se marcarán
            //    automáticamente como 'Added'.
            _context.Inspections.Add(inspection);

            // 2. Guardamos los cambios.
            var entries = await _context.SaveChangesAsync();

            _logger.LogInformation("Inspección {InspectionId} guardada con {Entries} entidades afectadas.",
                inspection.Id, entries);

            return inspection; // Devolvemos el objeto, ahora con su Id validado por la BBDD.
        }

        // --- MÉTODOS DE LECTURA (Get) ---

        public async Task<Inspection?> GetInspectionByIdAsync(Guid inspectionId)
        {
            // Usamos .Include para cargar el detalle de Findings y User en la misma consulta.
            return await _context.Inspections
                .Include(i => i.Findings)
                .Include(i => i.User) // Incluir User para obtener UserName
                .FirstOrDefaultAsync(i => i.Id == inspectionId);
        }

        public async Task<IReadOnlyList<Inspection>> GetInspectionsByOrganizationAsync(Guid organizationId)
        {
            // El filtro Multi-Tenant es crítico: solo se devuelve lo que le pertenece a esta Org.
            var inspections = await _context.Inspections
                .Include(i => i.Findings) // Incluimos los detalles
                .Include(i => i.User) // Incluir User para obtener UserName
                .Include(i => i.AffiliatedCompany) // Incluir Empresa Afiliada
                .Where(i => i.OrganizationId == organizationId)
                .OrderByDescending(i => i.AnalysisDate) // Las más recientes primero
                .AsNoTracking() // Esto es una consulta de lectura, hacemos que sea más rápida.
                .ToListAsync();

            return inspections;
        }

        public async Task<Guid?> GetFirstActiveAffiliatedCompanyIdAsync(Guid organizationId)
        {
            return await _context.AffiliatedCompanies
                .Where(ac => ac.IsActive && ac.OrganizationId == organizationId)
                .Select(ac => ac.Id)
                .FirstOrDefaultAsync();
        }
    }
}