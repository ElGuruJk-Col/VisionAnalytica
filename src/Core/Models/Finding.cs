// En: src/Core/Models/Finding.cs
// (¡NUEVO ARCHIVO!)

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Entidad de BBDD que representa un hallazgo específico dentro de una inspección.
    /// Es un reflejo persistente del HallazgoItem del resultado de la IA.
    /// </summary>
    public class Finding
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty; // Descripcion del Hallazgo

        [Required]
        [MaxLength(50)]
        public string RiskLevel { get; set; } = string.Empty; // NivelRiesgo (ej. ALTO, MEDIO, BAJO)

        [Required]
        [MaxLength(500)]
        public string CorrectiveAction { get; set; } = string.Empty; // AccionCorrectiva

        [MaxLength(500)]
        public string PreventiveAction { get; set; } = string.Empty; // AccionPreventiva

        // --- Clave Foránea (Relación N:1 con Photo) ---

        /// <summary>
        /// ID de la foto a la que pertenece este hallazgo.
        /// Cada hallazgo está asociado a una foto específica analizada por la IA.
        /// </summary>
        [Required]
        public Guid PhotoId { get; set; }
        [ForeignKey(nameof(PhotoId))]
        public virtual Photo Photo { get; set; } = null!; // Referencia a la foto
    }
}