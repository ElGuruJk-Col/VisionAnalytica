namespace VisioAnalytica.Core.Models.Dtos;

/// <summary>
/// DTO para obtener la configuración de optimización de imágenes de una organización.
/// </summary>
public class OrganizationSettingsDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public bool EnableImageOptimization { get; set; } = true;
    public int MaxImageWidth { get; set; } = 1920;
    public int ImageQuality { get; set; } = 85;
    public bool GenerateThumbnails { get; set; } = true;
    public int ThumbnailWidth { get; set; } = 400;
    public int ThumbnailQuality { get; set; } = 70;
}

/// <summary>
/// DTO para actualizar la configuración de optimización de imágenes de una organización.
/// </summary>
public class UpdateOrganizationSettingsDto
{
    public bool EnableImageOptimization { get; set; }
    public int MaxImageWidth { get; set; }
    public int ImageQuality { get; set; }
    public bool GenerateThumbnails { get; set; }
    public int ThumbnailWidth { get; set; }
    public int ThumbnailQuality { get; set; }
}

