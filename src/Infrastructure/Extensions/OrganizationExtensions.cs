using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Extensions;

/// <summary>
/// Extensiones para la entidad Organization.
/// </summary>
public static class OrganizationExtensions
{
    /// <summary>
    /// Crea la configuración por defecto para una organización si no existe.
    /// </summary>
    /// <param name="context">Contexto de base de datos</param>
    /// <param name="organizationId">ID de la organización</param>
    /// <param name="logger">Logger opcional para registrar la creación</param>
    /// <returns>La configuración creada o existente</returns>
    public static async Task<OrganizationSettings> EnsureDefaultSettingsAsync(
        this VisioAnalyticaDbContext context,
        Guid organizationId,
        ILogger? logger = null)
    {
        var existingSettings = await context.OrganizationSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId);

        if (existingSettings != null)
        {
            return existingSettings;
        }

        // Crear configuración por defecto con valores recomendados
        var defaultSettings = new OrganizationSettings
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            EnableImageOptimization = true,      // ✅ Habilitado por defecto
            MaxImageWidth = 1920,                // 📐 Full HD (balance entre calidad y tamaño)
            ImageQuality = 85,                    // 🎨 Alta calidad pero comprimida
            GenerateThumbnails = true,            // ✅ Habilitado por defecto
            ThumbnailWidth = 400,                 // 📐 Tamaño pequeño para carga rápida
            ThumbnailQuality = 70,                // 🎨 Calidad media para thumbnails
            CreatedAt = DateTime.UtcNow
        };

        context.OrganizationSettings.Add(defaultSettings);
        await context.SaveChangesAsync();

        logger?.LogInformation(
            "Configuración por defecto creada automáticamente para organización {OrgId}. " +
            "MaxWidth: {MaxWidth}, Quality: {Quality}, ThumbnailWidth: {ThumbWidth}",
            organizationId, defaultSettings.MaxImageWidth, defaultSettings.ImageQuality, 
            defaultSettings.ThumbnailWidth);

        return defaultSettings;
    }
}

