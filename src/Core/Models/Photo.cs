using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VisioAnalytica.Core.Models;

/// <summary>
/// Entidad que representa una foto capturada dentro de una inspección.
/// Una inspección puede tener múltiples fotos, y cada foto puede ser analizada individualmente.
/// </summary>
public class Photo
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// ID de la inspección a la que pertenece esta foto.
    /// </summary>
    [Required]
    public Guid InspectionId { get; set; }
    
    [ForeignKey(nameof(InspectionId))]
    public virtual Inspection Inspection { get; set; } = null!;

    /// <summary>
    /// URL o ruta de la imagen almacenada.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora en que se capturó la foto.
    /// </summary>
    [Required]
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Descripción opcional de la foto.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Indica si esta foto ha sido analizada.
    /// </summary>
    public bool IsAnalyzed { get; set; } = false;

    /// <summary>
    /// ID de la inspección de análisis asociada (si fue analizada).
    /// Una foto analizada genera una nueva inspección con los hallazgos.
    /// </summary>
    public Guid? AnalysisInspectionId { get; set; }
    
    [ForeignKey(nameof(AnalysisInspectionId))]
    public virtual Inspection? AnalysisInspection { get; set; }
}

