using System.ComponentModel.DataAnnotations;

namespace VisioAnalytica.Core.Models.Dtos
{
    /// <summary>
    /// DTO para crear una empresa afiliada.
    /// </summary>
    public class CreateAffiliatedCompanyDto
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// NIT o identificación fiscal de la empresa.
        /// </summary>
        [StringLength(50, ErrorMessage = "El NIT no puede exceder 50 caracteres")]
        public string? TaxId { get; set; }

        /// <summary>
        /// Dirección de la empresa.
        /// </summary>
        [StringLength(500, ErrorMessage = "La dirección no puede exceder 500 caracteres")]
        public string? Address { get; set; }

        /// <summary>
        /// Teléfono de contacto.
        /// </summary>
        [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
        public string? Phone { get; set; }

        /// <summary>
        /// Email de contacto.
        /// </summary>
        [EmailAddress(ErrorMessage = "El email no es válido")]
        [StringLength(100, ErrorMessage = "El email no puede exceder 100 caracteres")]
        public string? Email { get; set; }

        /// <summary>
        /// ID de la organización a la que pertenece esta empresa afiliada.
        /// </summary>
        [Required(ErrorMessage = "La organización es requerida")]
        public Guid OrganizationId { get; set; }
    }

    /// <summary>
    /// DTO para actualizar una empresa afiliada.
    /// </summary>
    public class UpdateAffiliatedCompanyDto
    {
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string? Name { get; set; }

        [StringLength(50, ErrorMessage = "El NIT no puede exceder 50 caracteres")]
        public string? TaxId { get; set; }

        [StringLength(500, ErrorMessage = "La dirección no puede exceder 500 caracteres")]
        public string? Address { get; set; }

        [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "El email no es válido")]
        [StringLength(100, ErrorMessage = "El email no puede exceder 100 caracteres")]
        public string? Email { get; set; }

        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// DTO para representar una empresa afiliada.
    /// </summary>
    public class AffiliatedCompanyDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? TaxId { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public Guid OrganizationId { get; set; }
        public string OrganizationName { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AssignedInspectorsCount { get; set; }
        public int InspectionsCount { get; set; }
    }
}

