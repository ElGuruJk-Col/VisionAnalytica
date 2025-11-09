using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Entidad de BBDD que representa una inspección/análisis de riesgos completado.
    /// Es la cabecera del informe.
    /// </summary>
    public class Inspection
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow; // Cuándo se guardó

        [Required]
        [MaxLength(255)]
        public string ImageUrl { get; set; } = string.Empty; // La URL o Path de la imagen (Blob Storage)

        // --- Claves Foráneas (Relaciones Multi-Tenant y Usuario) ---

        // El inspector que realizó el análisis (Relación 1:N con User)
        [Required]
        public Guid UserId { get; set; }
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!; // Referencia al usuario de Identity

        // Relación 1:N con la Organización (para consultas Multi-Tenant rápidas)
        [Required]
        public Guid OrganizationId { get; set; }
        [ForeignKey(nameof(OrganizationId))]
        public virtual Organization Organization { get; set; } = null!;

        // Propiedad de Navegación: Una Inspección tiene MUCHOS Hallazgos
        public virtual ICollection<Finding> Findings { get; set; } = new HashSet<Finding>();
    }
}