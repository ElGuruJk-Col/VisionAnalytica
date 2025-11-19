using System;
using System.Collections.Generic;

namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Representa una empresa afiliada que será auditada por inspectores.
    /// Las empresas afiliadas pertenecen a una organización cliente y son
    /// asignadas a uno o varios inspectores para realizar auditorías.
    /// </summary>
    public class AffiliatedCompany
    {
        /// <summary>
        /// Clave primaria. Usamos Guid para asegurar unicidad global.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Nombre de la empresa afiliada (ej. "Constructora ABC S.A.S").
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// NIT o identificación fiscal de la empresa.
        /// </summary>
        public string? TaxId { get; set; }

        /// <summary>
        /// Dirección de la empresa.
        /// </summary>
        public string? Address { get; set; }

        /// <summary>
        /// Teléfono de contacto de la empresa.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Email de contacto de la empresa.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Clave foránea que vincula esta empresa afiliada con la organización cliente
        /// que la contrató para ser auditada.
        /// </summary>
        public Guid OrganizationId { get; set; }

        /// <summary>
        /// Propiedad de navegación. Una empresa afiliada pertenece a UNA organización.
        /// </summary>
        public virtual Organization Organization { get; set; } = null!;

        /// <summary>
        /// Indica si la empresa afiliada está activa.
        /// Las empresas inactivas no pueden ser asignadas a nuevas inspecciones.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Fecha y hora de creación del registro.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ID del usuario que creó esta empresa afiliada (normalmente un Admin).
        /// </summary>
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// Propiedad de navegación. Inspectores asignados a esta empresa afiliada.
        /// Relación Many-to-Many a través de InspectorAffiliatedCompany.
        /// </summary>
        public virtual ICollection<User> AssignedInspectors { get; set; } = new HashSet<User>();

        /// <summary>
        /// Propiedad de navegación. Inspecciones realizadas a esta empresa afiliada.
        /// </summary>
        public virtual ICollection<Inspection> Inspections { get; set; } = new HashSet<Inspection>();

        public AffiliatedCompany()
        {
            AssignedInspectors = new HashSet<User>();
            Inspections = new HashSet<Inspection>();
        }
    }
}

