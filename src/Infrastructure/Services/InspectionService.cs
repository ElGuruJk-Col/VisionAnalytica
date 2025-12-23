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
    ILogger<InspectionService> logger,
    ServerImageOptimizationService? imageOptimizationService = null) : IInspectionService
{
    private readonly VisioAnalyticaDbContext _context = context;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IBackgroundJobClient _backgroundJobClient = backgroundJobClient;
    private readonly ILogger<InspectionService> _logger = logger;
    private readonly ServerImageOptimizationService? _imageOptimizationService = imageOptimizationService;

    public async Task<InspectionDto> CreateInspectionAsync(CreateInspectionDto request, Guid userId, Guid organizationId)
    {
        // Verificar que la empresa afiliada pertenezca a la organización
        var company = await _context.AffiliatedCompanies
            .FirstOrDefaultAsync(ac => ac.Id == request.AffiliatedCompanyId && 
                                      ac.OrganizationId == organizationId && 
                                      ac.IsActive) ?? throw new InvalidOperationException("La empresa cliente especificada no existe o no está activa.");

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

        // Obtener configuración de la organización para optimización
        var orgSettings = await _context.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

        // Valores por defecto si no hay configuración
        var generateThumbnails = orgSettings?.GenerateThumbnails ?? true;
        var thumbnailWidth = orgSettings?.ThumbnailWidth ?? 400;
        var thumbnailQuality = orgSettings?.ThumbnailQuality ?? 70;

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

                // Generar thumbnail automáticamente si está habilitado
                if (generateThumbnails && _imageOptimizationService != null)
                {
                    try
                    {
                        var thumbnailBytes = _imageOptimizationService.GenerateThumbnail(
                            imageBytes, 
                            thumbnailWidth, 
                            thumbnailQuality);

                        if (thumbnailBytes != null)
                        {
                            // Extraer nombre del archivo de la URL de la imagen
                            var fileName = imageUrl.Split('/').Last().Split('?').First();
                            await _fileStorage.SaveThumbnailAsync(thumbnailBytes, fileName, organizationId);
                            _logger.LogDebug("Thumbnail generado automáticamente para imagen: {ImageUrl}", imageUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        // No fallar si el thumbnail no se puede generar, solo loguear
                        _logger.LogWarning(ex, "No se pudo generar thumbnail para imagen: {ImageUrl}", imageUrl);
                    }
                }

                var photo = new Photo
                {
                    Id = Guid.NewGuid(),
                    InspectionId = inspection.Id,
                    ImageUrl = imageUrl,
                    CapturedAt = photoDto.CapturedAt,
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
            .Include(i => i.Photos)
                .ThenInclude(p => p.Findings) // ✅ CORRECCIÓN: Include Findings de las fotos
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

        return [.. inspections.Select(i => MapToDto(i))];
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
            .Include(i => i.Photos)
                .ThenInclude(p => p.Findings) // ✅ CORRECCIÓN: Include Findings de las fotos
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
        // ✅ CORRECCIÓN: Los hallazgos están en las fotos, no en la inspección
        var findingsCount = i.Photos?.Sum(p => p.Findings?.Count ?? 0) ?? 0;
        
        _logger.LogDebug(
            "Inspección {InspectionId}: {PhotosCount} fotos totales, {AnalyzedCount} analizadas, {FindingsCount} hallazgos totales",
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
                p.IsAnalyzed
            )).ToList() ?? []
        );
    }

    public async Task<InspectionDto?> GetInspectionByIdAsync(Guid inspectionId, Guid userId, Guid organizationId)
    {
        var inspection = await _context.Inspections
            .Include(i => i.AffiliatedCompany)
            .Include(i => i.Photos)
                .ThenInclude(p => p.Findings) // ✅ CORRECCIÓN: Incluir Findings de cada foto
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
        // ✅ CORRECCIÓN: Los hallazgos están en las fotos, no en la inspección
        var findingsCount = inspection.Photos?.Sum(p => p.Findings?.Count ?? 0) ?? 0;
        
        _logger.LogDebug(
            "Inspección {InspectionId}: {PhotosCount} fotos totales, {AnalyzedCount} analizadas, {FindingsCount} hallazgos totales",
            inspectionId, photosCount, analyzedCount, findingsCount);

        // Logging detallado de cada foto para diagnosticar
        if (inspection.Photos != null)
        {
            foreach (var photo in inspection.Photos)
            {
                _logger.LogDebug(
                    "Foto {PhotoId}: IsAnalyzed={IsAnalyzed}, Hallazgos={FindingsCount}",
                    photo.Id, photo.IsAnalyzed, photo.Findings?.Count ?? 0);
            }
        }

        var photosDto = inspection.Photos?.Select(p => 
        {
            _logger.LogDebug(
                "Mapeando foto {PhotoId} a DTO: IsAnalyzed={IsAnalyzed}, Hallazgos={FindingsCount}",
                p.Id, p.IsAnalyzed, p.Findings?.Count ?? 0);
            
            return new PhotoInfoDto(
                p.Id,
                p.ImageUrl,
                p.CapturedAt,
                p.IsAnalyzed
            );
        }).ToList() ?? [];

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
            photosDto
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
                                     i.OrganizationId == organizationId) ?? throw new InvalidOperationException("Inspección no encontrada.");
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

    /// <summary>
    /// Obtiene los hallazgos de una foto específica.
    /// </summary>
    public async Task<List<FindingDetailDto>> GetPhotoFindingsAsync(Guid photoId, Guid userId, Guid organizationId)
    {
        // Verificar que la foto pertenezca a una inspección del usuario y organización
        var photo = await _context.Photos
            .Include(p => p.Findings)
            .Include(p => p.Inspection)
            .FirstOrDefaultAsync(p => p.Id == photoId &&
                                     p.Inspection.UserId == userId &&
                                     p.Inspection.OrganizationId == organizationId) ?? throw new InvalidOperationException("Foto no encontrada o no pertenece al usuario/organización.");
        return [.. photo.Findings.Select(f => new FindingDetailDto(
            f.Id,
            f.Description,
            f.RiskLevel,
            f.CorrectiveAction,
            f.PreventiveAction ?? string.Empty
        ))];
    }
}

