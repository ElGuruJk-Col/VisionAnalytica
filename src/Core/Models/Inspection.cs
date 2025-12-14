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

        // Relación 1:N con la Empresa Afiliada auditada
        [Required]
        public Guid AffiliatedCompanyId { get; set; }
        [ForeignKey(nameof(AffiliatedCompanyId))]
        public virtual AffiliatedCompany AffiliatedCompany { get; set; } = null!;

        /// <summary>
        /// Estado de la inspección: Draft, PhotosCaptured, Analyzing, Completed, Failed
        /// </summary>
        [MaxLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, PhotosCaptured, Analyzing, Completed, Failed

        /// <summary>
        /// Fecha y hora en que se inició la inspección.
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha y hora en que se completó el análisis.
        /// Null si aún está en proceso.
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Fotos capturadas en esta inspección.
        /// Cada foto puede tener sus propios hallazgos (Findings).
        /// </summary>
        public virtual ICollection<Photo> Photos { get; set; } = new HashSet<Photo>();
    }
}