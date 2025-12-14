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
    /// Indica si esta foto ha sido analizada.
    /// </summary>
    public bool IsAnalyzed { get; set; } = false;

    /// <summary>
    /// Hallazgos encontrados en esta foto por el análisis de IA.
    /// Relación 1:N con Finding.
    /// </summary>
    public virtual ICollection<Finding> Findings { get; set; } = new HashSet<Finding>();
}

