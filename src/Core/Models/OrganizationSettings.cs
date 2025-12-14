using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Configuración específica de una organización para optimización de imágenes.
    /// Permite que cada cliente configure si desea optimizar imágenes y cómo.
    /// </summary>
    public class OrganizationSettings
    {
        /// <summary>
        /// Clave primaria.
        /// </summary>
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Clave foránea a la organización.
        /// Relación 1:1 con Organization.
        /// </summary>
        [Required]
        public Guid OrganizationId { get; set; }

        /// <summary>
        /// Propiedad de navegación a la organización.
        /// </summary>
        [ForeignKey(nameof(OrganizationId))]
        public virtual Organization Organization { get; set; } = null!;

        /// <summary>
        /// Indica si se debe optimizar las imágenes antes de almacenarlas.
        /// Si es false, las imágenes se guardan tal como se capturan.
        /// </summary>
        public bool EnableImageOptimization { get; set; } = true;

        /// <summary>
        /// Ancho máximo en píxeles para las imágenes optimizadas.
        /// Si la imagen es más ancha, se redimensiona manteniendo el aspect ratio.
        /// </summary>
        public int MaxImageWidth { get; set; } = 1920;

        /// <summary>
        /// Calidad de compresión JPEG (0-100).
        /// 100 = máxima calidad, 0 = máxima compresión.
        /// </summary>
        [Range(0, 100)]
        public int ImageQuality { get; set; } = 85;

        /// <summary>
        /// Indica si se deben generar thumbnails de las imágenes.
        /// Los thumbnails son versiones pequeñas para carga rápida.
        /// </summary>
        public bool GenerateThumbnails { get; set; } = true;

        /// <summary>
        /// Ancho máximo en píxeles para los thumbnails.
        /// </summary>
        public int ThumbnailWidth { get; set; } = 400;

        /// <summary>
        /// Calidad de compresión para thumbnails (0-100).
        /// Generalmente más baja que ImageQuality para reducir tamaño.
        /// </summary>
        [Range(0, 100)]
        public int ThumbnailQuality { get; set; } = 70;

        /// <summary>
        /// Fecha y hora de creación del registro.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha y hora de última actualización.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// ID del usuario que creó esta configuración.
        /// </summary>
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// ID del usuario que actualizó esta configuración por última vez.
        /// </summary>
        public Guid? UpdatedBy { get; set; }
    }
}

