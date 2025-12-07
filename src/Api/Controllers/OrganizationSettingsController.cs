using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VisioAnalytica.Core.Constants;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;
using VisioAnalytica.Infrastructure.Services;

namespace VisioAnalytica.Api.Controllers;

/// <summary>
/// Controlador para gestionar la configuración de optimización de imágenes de una organización.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class OrganizationSettingsController : ControllerBase
{
    private readonly VisioAnalyticaDbContext _context;
    private readonly ILogger<OrganizationSettingsController> _logger;
    private readonly IFileStorage _fileStorage;
    private readonly ServerImageOptimizationService _imageOptimizationService;

    public OrganizationSettingsController(
        VisioAnalyticaDbContext context,
        ILogger<OrganizationSettingsController> logger,
        IFileStorage fileStorage,
        ServerImageOptimizationService imageOptimizationService)
    {
        _context = context;
        _logger = logger;
        _fileStorage = fileStorage;
        _imageOptimizationService = imageOptimizationService;
    }

    /// <summary>
    /// Obtiene el ID de la organización del usuario autenticado desde el token JWT.
    /// </summary>
    private Guid? GetOrganizationIdFromClaims()
    {
        var orgIdString = User.FindFirst("org_id")?.Value;
        if (string.IsNullOrWhiteSpace(orgIdString) || !Guid.TryParse(orgIdString, out var organizationId))
        {
            return null;
        }
        return organizationId;
    }

    /// <summary>
    /// Obtiene la configuración de optimización de imágenes de la organización del usuario autenticado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrganizationSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettings()
    {
        var orgId = GetOrganizationIdFromClaims();
        if (!orgId.HasValue)
        {
            return Unauthorized("No se pudo determinar la organización del usuario.");
        }

        var settings = await _context.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId.Value);

        if (settings == null)
        {
            // Crear configuración por defecto si no existe
            settings = new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId.Value,
                EnableImageOptimization = true,
                MaxImageWidth = 1920,
                ImageQuality = 85,
                GenerateThumbnails = true,
                ThumbnailWidth = 400,
                ThumbnailQuality = 70,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.OrganizationSettings.Add(settings);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Configuración por defecto creada para organización {OrgId}", orgId.Value);
        }

        var dto = new OrganizationSettingsDto
        {
            Id = settings.Id,
            OrganizationId = settings.OrganizationId,
            EnableImageOptimization = settings.EnableImageOptimization,
            MaxImageWidth = settings.MaxImageWidth,
            ImageQuality = settings.ImageQuality,
            GenerateThumbnails = settings.GenerateThumbnails,
            ThumbnailWidth = settings.ThumbnailWidth,
            ThumbnailQuality = settings.ThumbnailQuality
        };

        return Ok(dto);
    }

    /// <summary>
    /// Actualiza la configuración de optimización de imágenes de la organización.
    /// Solo SuperAdmin puede actualizar la configuración.
    /// </summary>
    [HttpPut]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(OrganizationSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateOrganizationSettingsDto dto)
    {
        var orgId = GetOrganizationIdFromClaims();
        if (!orgId.HasValue)
        {
            return Unauthorized("No se pudo determinar la organización del usuario.");
        }

        var settings = await _context.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId.Value);

        if (settings == null)
        {
            // Crear nueva configuración
            settings = new OrganizationSettings
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId.Value,
                CreatedAt = DateTime.UtcNow
            };
            _context.OrganizationSettings.Add(settings);
        }

        // Actualizar valores
        settings.EnableImageOptimization = dto.EnableImageOptimization;
        settings.MaxImageWidth = dto.MaxImageWidth;
        settings.ImageQuality = dto.ImageQuality;
        settings.GenerateThumbnails = dto.GenerateThumbnails;
        settings.ThumbnailWidth = dto.ThumbnailWidth;
        settings.ThumbnailQuality = dto.ThumbnailQuality;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Configuración actualizada para organización {OrgId}. " +
            "Optimization: {Enable}, MaxWidth: {MaxWidth}, Quality: {Quality}, " +
            "Thumbnails: {Thumbnails}, ThumbnailWidth: {ThumbWidth}, ThumbnailQuality: {ThumbQuality}",
            orgId.Value, dto.EnableImageOptimization, dto.MaxImageWidth, dto.ImageQuality,
            dto.GenerateThumbnails, dto.ThumbnailWidth, dto.ThumbnailQuality);

        var responseDto = new OrganizationSettingsDto
        {
            Id = settings.Id,
            OrganizationId = settings.OrganizationId,
            EnableImageOptimization = settings.EnableImageOptimization,
            MaxImageWidth = settings.MaxImageWidth,
            ImageQuality = settings.ImageQuality,
            GenerateThumbnails = settings.GenerateThumbnails,
            ThumbnailWidth = settings.ThumbnailWidth,
            ThumbnailQuality = settings.ThumbnailQuality
        };

        return Ok(responseDto);
    }

    /// <summary>
    /// Genera thumbnails retroactivamente para todas las imágenes existentes de la organización.
    /// Solo SuperAdmin puede ejecutar esta operación.
    /// Si se proporciona organizationId, SuperAdmin puede generar thumbnails para cualquier organización.
    /// Si no se proporciona, usa la organización del usuario autenticado.
    /// </summary>
    [HttpPost("generate-thumbnails")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateThumbnailsRetroactively([FromQuery] Guid? organizationId = null)
    {
        Guid targetOrgId;
        
        // Si se proporciona organizationId, SuperAdmin puede procesar cualquier organización
        if (organizationId.HasValue)
        {
            // Verificar que la organización existe
            var orgExists = await _context.Organizations
                .AnyAsync(o => o.Id == organizationId.Value);
            
            if (!orgExists)
            {
                return BadRequest(new { 
                    message = $"La organización con ID {organizationId.Value} no existe.",
                    error = "Organization not found"
                });
            }
            
            targetOrgId = organizationId.Value;
            _logger.LogInformation(
                "SuperAdmin generando thumbnails para organización {OrgId} (especificada explícitamente)", 
                targetOrgId);
        }
        else
        {
            // Si no se proporciona, usar la organización del usuario autenticado
            var orgId = GetOrganizationIdFromClaims();
            if (!orgId.HasValue)
            {
                return Unauthorized("No se pudo determinar la organización del usuario.");
            }
            targetOrgId = orgId.Value;
        }

        // Obtener configuración de la organización objetivo
        var settings = await _context.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == targetOrgId);

        // Si no existe configuración, usar valores por defecto
        var thumbnailWidth = settings?.ThumbnailWidth ?? 400;
        var thumbnailQuality = settings?.ThumbnailQuality ?? 70;
        var generateThumbnails = settings?.GenerateThumbnails ?? true;

        if (!generateThumbnails && settings != null)
        {
            return BadRequest(new { 
                message = "La generación de thumbnails está deshabilitada para esta organización.",
                error = "Thumbnails disabled"
            });
        }

        // Obtener todas las fotos de la organización objetivo
        var photos = await _context.Photos
            .Include(p => p.Inspection)
            .Where(p => p.Inspection!.OrganizationId == targetOrgId)
            .ToListAsync();

        if (photos.Count == 0)
        {
            return Ok(new
            {
                message = "No se encontraron fotos para procesar",
                organizationId = targetOrgId,
                totalPhotos = 0,
                processed = 0,
                skipped = 0,
                errors = 0
            });
        }

        var processedCount = 0;
        var errorCount = 0;
        var skippedCount = 0;

        foreach (var photo in photos)
        {
            try
            {
                // Verificar si el thumbnail ya existe
                var imageUrl = photo.ImageUrl;
                if (imageUrl.Contains("/api/v1/file/images/"))
                {
                    var parts = imageUrl.Replace("/api/v1/file/images/", "").Split('/');
                    if (parts.Length >= 2)
                    {
                        var fileName = parts[1].Split('?').First();
                        var thumbnailFileName = $"thumb_{fileName}";
                        var thumbnailUrl = $"/api/v1/file/images/{parts[0]}/thumbnails/{thumbnailFileName}";

                        // Intentar leer el thumbnail para ver si existe
                        var existingThumbnail = await _fileStorage.ReadImageAsync(thumbnailUrl);
                        if (existingThumbnail != null && existingThumbnail.Length > 0)
                        {
                            skippedCount++;
                            continue; // Ya existe, saltar
                        }
                    }
                }

                // Leer la imagen original
                var imageBytes = await _fileStorage.ReadImageAsync(imageUrl);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    _logger.LogWarning("No se pudo leer la imagen para foto {PhotoId}: {ImageUrl}", photo.Id, imageUrl);
                    errorCount++;
                    continue;
                }

                // Generar thumbnail
                var thumbnailBytes = _imageOptimizationService.GenerateThumbnail(
                    imageBytes,
                    thumbnailWidth,
                    thumbnailQuality);

                if (thumbnailBytes == null || thumbnailBytes.Length == 0)
                {
                    _logger.LogWarning("No se pudo generar thumbnail para foto {PhotoId}", photo.Id);
                    errorCount++;
                    continue;
                }

                // Guardar thumbnail
                var imageFileName = imageUrl.Split('/').Last().Split('?').First();
                await _fileStorage.SaveThumbnailAsync(thumbnailBytes, imageFileName, targetOrgId);

                processedCount++;
                
                // Log cada 10 imágenes procesadas para no saturar los logs
                if (processedCount % 10 == 0)
                {
                    _logger.LogInformation(
                        "Progreso: {ProcessedCount}/{TotalCount} thumbnails generados para organización {OrgId}",
                        processedCount, photos.Count, targetOrgId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar thumbnail para foto {PhotoId}", photo.Id);
                errorCount++;
            }
        }

        _logger.LogInformation(
            "Generación de thumbnails retroactiva completada para organización {OrgId}. " +
            "Procesadas: {ProcessedCount}, Omitidas: {SkippedCount}, Errores: {ErrorCount}",
            targetOrgId, processedCount, skippedCount, errorCount);

        return Ok(new
        {
            message = "Generación de thumbnails completada",
            organizationId = targetOrgId,
            totalPhotos = photos.Count,
            processed = processedCount,
            skipped = skippedCount,
            errors = errorCount
        });
    }
}

