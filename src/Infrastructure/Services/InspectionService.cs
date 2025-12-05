using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services;

/// <summary>
/// Servicio para gestionar inspecciones con múltiples fotos.
/// </summary>
public class InspectionService(
    VisioAnalyticaDbContext context,
    IFileStorage fileStorage,
    IBackgroundJobClient backgroundJobClient,
    ILogger<InspectionService> logger) : IInspectionService
{
    private readonly VisioAnalyticaDbContext _context = context;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;
    private readonly ILogger<InspectionService> _logger = logger;

    public async Task<InspectionDto> CreateInspectionAsync(CreateInspectionDto request, Guid userId, Guid organizationId)
    {
        // Verificar que la empresa afiliada pertenezca a la organización
        var company = await _context.AffiliatedCompanies
            .FirstOrDefaultAsync(ac => ac.Id == request.AffiliatedCompanyId && 
                                      ac.OrganizationId == organizationId && 
                                      ac.IsActive);

        if (company == null)
        {
            throw new InvalidOperationException("La empresa cliente especificada no existe o no está activa.");
        }

        // Crear la inspección
        var inspection = new Inspection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            AffiliatedCompanyId = request.AffiliatedCompanyId,
            Status = "PhotosCaptured",
            StartedAt = DateTime.UtcNow
        };

        _context.Inspections.Add(inspection);

        // Guardar las fotos
        var photos = new List<Photo>();
        foreach (var photoDto in request.Photos)
        {
            try
            {
                // Convertir Base64 a bytes
                var imageBytes = Convert.FromBase64String(photoDto.ImageBase64);

                // Guardar la imagen
                var imageUrl = await _fileStorage.SaveImageAsync(imageBytes, null, organizationId);

                var photo = new Photo
                {
                    Id = Guid.NewGuid(),
                    InspectionId = inspection.Id,
                    ImageUrl = imageUrl,
                    CapturedAt = photoDto.CapturedAt,
                    Description = photoDto.Description,
                    IsAnalyzed = false
                };

                photos.Add(photo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar foto en inspección {InspectionId}", inspection.Id);
                throw new InvalidOperationException($"Error al guardar una de las fotos: {ex.Message}", ex);
            }
        }

        _context.Photos.AddRange(photos);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Inspección {InspectionId} creada con {PhotoCount} fotos", inspection.Id, photos.Count);
        
        // Verificar que las fotos se guardaron correctamente
        var savedPhotosCount = await _context.Photos.CountAsync(p => p.InspectionId == inspection.Id);
        _logger.LogInformation("Verificación: {SavedCount} fotos guardadas en BD para inspección {InspectionId}", 
            savedPhotosCount, inspection.Id);
        
        if (savedPhotosCount != photos.Count)
        {
            _logger.LogWarning("DISCREPANCIA: Se intentaron guardar {ExpectedCount} fotos pero solo {ActualCount} están en BD", 
                photos.Count, savedPhotosCount);
        }

        // Recargar la inspección con las fotos para asegurar que se incluyan
        var reloadedInspection = await _context.Inspections
            .Include(i => i.AffiliatedCompany)
            .Include(i => i.Photos)
            .FirstOrDefaultAsync(i => i.Id == inspection.Id);
        
        if (reloadedInspection != null)
        {
            _logger.LogInformation("Inspección recargada: {PhotoCount} fotos en la entidad", reloadedInspection.Photos.Count);
        }

        return await GetInspectionByIdAsync(inspection.Id, userId, organizationId) 
            ?? throw new InvalidOperationException("Error al recuperar la inspección creada.");
    }

    public async Task<List<InspectionDto>> GetMyInspectionsAsync(Guid userId, Guid organizationId, Guid? affiliatedCompanyId = null)
    {
        var query = _context.Inspections
            .Include(i => i.AffiliatedCompany)
            .Include(i => i.Photos)
            .Where(i => i.UserId == userId && i.OrganizationId == organizationId);

        if (affiliatedCompanyId.HasValue)
        {
            query = query.Where(i => i.AffiliatedCompanyId == affiliatedCompanyId.Value);
        }

        var inspections = await query
            .Include(i => i.Findings) // Include Findings to count them
            .OrderByDescending(i => i.StartedAt)
            .ToListAsync();

        _logger.LogInformation("Obteniendo {Count} inspecciones para usuario {UserId}", inspections.Count, userId);
        
        // Verificar que las fotos se cargaron correctamente
        foreach (var inspection in inspections)
        {
            var photosInMemory = inspection.Photos?.Count ?? 0;
            var photosInDb = await _context.Photos.CountAsync(p => p.InspectionId == inspection.Id);
            
            if (photosInMemory != photosInDb)
            {
                _logger.LogWarning(
                    "DISCREPANCIA en inspección {InspectionId}: {MemoryCount} fotos en memoria vs {DbCount} en BD. Recargando...",
                    inspection.Id, photosInMemory, photosInDb);
                
                // Forzar recarga de las fotos
                await _context.Entry(inspection).Collection(i => i.Photos).LoadAsync();
            }
        }

        return inspections.Select(i => MapToDto(i)).ToList();
    }
    
    public async Task<PagedResult<InspectionDto>> GetMyInspectionsPagedAsync(
        Guid userId, 
        Guid organizationId, 
        int pageNumber = 1, 
        int pageSize = 20, 
        Guid? affiliatedCompanyId = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Límite máximo
        
        var query = _context.Inspections
            .Include(i => i.AffiliatedCompany)
            .Include(i => i.Photos)
            .Where(i => i.UserId == userId && i.OrganizationId == organizationId);

        if (affiliatedCompanyId.HasValue)
        {
            query = query.Where(i => i.AffiliatedCompanyId == affiliatedCompanyId.Value);
        }

        // Obtener total antes de paginar
        var totalCount = await query.CountAsync();
        
        // Aplicar paginación
        var inspections = await query
            .Include(i => i.Findings)
            .OrderByDescending(i => i.StartedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation(
            "Obteniendo página {PageNumber} de inspecciones para usuario {UserId}. Total: {TotalCount}, Página: {PageSize}",
            pageNumber, userId, totalCount, pageSize);
        
        // Verificar que las fotos se cargaron correctamente
        foreach (var inspection in inspections)
        {
            var photosInMemory = inspection.Photos?.Count ?? 0;
            var photosInDb = await _context.Photos.CountAsync(p => p.InspectionId == inspection.Id);
            
            if (photosInMemory != photosInDb)
            {
                _logger.LogWarning(
                    "DISCREPANCIA en inspección {InspectionId}: {MemoryCount} fotos en memoria vs {DbCount} en BD. Recargando...",
                    inspection.Id, photosInMemory, photosInDb);
                
                await _context.Entry(inspection).Collection(i => i.Photos).LoadAsync();
            }
        }

        var items = inspections.Select(i => MapToDto(i)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        
        return new PagedResult<InspectionDto>(
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            pageNumber > 1,
            pageNumber < totalPages
        );
    }
    
    private InspectionDto MapToDto(Core.Models.Inspection i)
    {
        // Asegurar que las fotos estén cargadas - usar null-conditional para evitar errores
        var photosCount = i.Photos?.Count ?? 0;
        var analyzedCount = i.Photos?.Count(p => p.IsAnalyzed) ?? 0;
        var findingsCount = i.Findings?.Count ?? 0;
        
        _logger.LogDebug(
            "Inspección {InspectionId}: {PhotosCount} fotos totales, {AnalyzedCount} analizadas, {FindingsCount} hallazgos",
            i.Id, photosCount, analyzedCount, findingsCount);

        return new InspectionDto(
            i.Id,
            i.AffiliatedCompanyId,
            i.AffiliatedCompany?.Name ?? "Sin nombre",
            i.Status,
            i.StartedAt,
            i.CompletedAt,
            photosCount,
            analyzedCount,
            findingsCount,
            i.Photos?.Select(p => new PhotoInfoDto(
                p.Id,
                p.ImageUrl,
                p.CapturedAt,
                p.Description,
                p.IsAnalyzed,
                p.AnalysisInspectionId
            )).ToList() ?? []
        );
    }

    public async Task<InspectionDto?> GetInspectionByIdAsync(Guid inspectionId, Guid userId, Guid organizationId)
    {
        var inspection = await _context.Inspections
            .Include(i => i.AffiliatedCompany)
            .Include(i => i.Photos)
            .FirstOrDefaultAsync(i => i.Id == inspectionId && 
                                     i.UserId == userId && 
                                     i.OrganizationId == organizationId);

        if (inspection == null)
        {
            _logger.LogWarning("Inspección {InspectionId} no encontrada para usuario {UserId}", inspectionId, userId);
            return null;
        }

        // Asegurar que las fotos estén cargadas - usar null-conditional para evitar errores
        var photosCount = inspection.Photos?.Count ?? 0;
        var analyzedCount = inspection.Photos?.Count(p => p.IsAnalyzed) ?? 0;
        
        _logger.LogDebug(
            "Inspección {InspectionId}: {PhotosCount} fotos totales, {AnalyzedCount} analizadas",
            inspectionId, photosCount, analyzedCount);

        var findingsCount = inspection.Findings?.Count ?? 0;

        return new InspectionDto(
            inspection.Id,
            inspection.AffiliatedCompanyId,
            inspection.AffiliatedCompany?.Name ?? "Sin nombre",
            inspection.Status,
            inspection.StartedAt,
            inspection.CompletedAt,
            photosCount,
            analyzedCount,
            findingsCount,
            inspection.Photos?.Select(p => new PhotoInfoDto(
                p.Id,
                p.ImageUrl,
                p.CapturedAt,
                p.Description,
                p.IsAnalyzed,
                p.AnalysisInspectionId
            )).ToList() ?? []
        );
    }

    public Task<string> StartAnalysisAsync(AnalyzeInspectionDto request, Guid userId, Guid organizationId)
    {
        // Encolar el análisis en segundo plano usando Hangfire
        var jobId = _backgroundJobClient.Enqueue<IAnalysisOrchestrator>(
            orchestrator => orchestrator.AnalyzeInspectionPhotosAsync(
                request.InspectionId,
                request.PhotoIds,
                userId));
        
        _logger.LogInformation(
            "Análisis de inspección {InspectionId} encolado con jobId {JobId}",
            request.InspectionId, jobId);
        
        return Task.FromResult(jobId);
    }

    public async Task<InspectionAnalysisStatusDto> GetAnalysisStatusAsync(Guid inspectionId, Guid userId, Guid organizationId)
    {
        var inspection = await _context.Inspections
            .Include(i => i.Photos)
            .FirstOrDefaultAsync(i => i.Id == inspectionId && 
                                     i.UserId == userId && 
                                     i.OrganizationId == organizationId);

        if (inspection == null)
        {
            throw new InvalidOperationException("Inspección no encontrada.");
        }

        var totalPhotos = inspection.Photos.Count;
        var analyzedPhotos = inspection.Photos.Count(p => p.IsAnalyzed);
        var pendingPhotos = totalPhotos - analyzedPhotos;

        return new InspectionAnalysisStatusDto(
            inspection.Id,
            inspection.Status,
            totalPhotos,
            analyzedPhotos,
            pendingPhotos,
            inspection.StartedAt,
            inspection.CompletedAt,
            null
        );
    }

    public async Task<List<FindingDetailDto>> GetInspectionFindingsAsync(Guid analysisInspectionId, Guid userId, Guid organizationId)
    {
        // Verificar que la inspección de análisis pertenezca al usuario y organización
        var analysisInspection = await _context.Inspections
            .Include(i => i.Findings)
            .FirstOrDefaultAsync(i => i.Id == analysisInspectionId &&
                                     i.UserId == userId &&
                                     i.OrganizationId == organizationId);

        if (analysisInspection == null)
        {
            throw new InvalidOperationException("Inspección de análisis no encontrada.");
        }

        return analysisInspection.Findings.Select(f => new FindingDetailDto(
            f.Id,
            f.Description,
            f.RiskLevel,
            f.CorrectiveAction,
            f.PreventiveAction ?? string.Empty
        )).ToList();
    }
}

