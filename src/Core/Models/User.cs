using Microsoft.AspNetCore.Identity;
using System;

namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Define la entidad de un Usuario en nuestro sistema.
    /// Esta clase es "promovida" para heredar de IdentityUser,
    /// dándonos toda la magia de ASP.NET Core Identity (seguridad,
    /// tokens, roles, hash de contraseñas) de forma gratuita.
    /// Usamos <Guid> para que nuestras Claves Primarias sean GUIDs.
    /// </summary>
    public class User : IdentityUser<Guid>
    {
        // IdentityUser ya provee:
        // - Id (lo forzamos a Guid)
        // - UserName
        // - Email
        // - PasswordHash
        // - PhoneNumber
        // - etc.

        // --- AÑADIMOS NUESTROS CAMPOS PERSONALIZADOS ---

        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;

        // --- LA MAGIA DEL MULTI-TENANT ---

        /// <summary>
        /// Clave foránea (Foreign Key) que vincula
        /// a este usuario con su organización.
        /// </summary>
        public Guid OrganizationId { get; set; }

        /// <summary>
        /// Propiedad de navegación (EF Core)
        /// Un usuario pertenece a UNA organización.
        /// </summary>
        public virtual Organization Organization { get; set; } = null!;

        // --- GESTIÓN DE CONTRASEÑAS Y ESTADO ---

        /// <summary>
        /// Indica si el usuario debe cambiar su contraseña en el próximo inicio de sesión.
        /// Se establece en true cuando se crea con contraseña provisional.
        /// </summary>
        public bool MustChangePassword { get; set; } = false;

        /// <summary>
        /// Fecha y hora en que el usuario cambió su contraseña por última vez.
        /// Null si nunca ha cambiado su contraseña.
        /// </summary>
        public DateTime? PasswordChangedAt { get; set; }

        /// <summary>
        /// Indica si el usuario está activo en el sistema.
        /// Los usuarios inactivos no pueden iniciar sesión.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Número de intentos fallidos de inicio de sesión.
        /// Se resetea a 0 cuando el login es exitoso.
        /// </summary>
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>
        /// Fecha y hora hasta la cual la cuenta está bloqueada.
        /// Null si la cuenta no está bloqueada.
        /// </summary>
        public DateTime? LockedUntil { get; set; }

        /// <summary>
        /// Fecha y hora de creación del usuario.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// ID del usuario que creó este usuario (puede ser SuperAdmin o Admin).
        /// Null si fue creado por el sistema.
        /// </summary>
        public Guid? CreatedBy { get; set; }

        /// <summary>
        /// ID del supervisor/administrador actual del inspector.
        /// Puede ser diferente de CreatedBy si el supervisor cambió.
        /// Null si no tiene supervisor asignado.
        /// </summary>
        public Guid? SupervisorId { get; set; }

        /// <summary>
        /// Propiedad de navegación al supervisor actual.
        /// </summary>
        public virtual User? Supervisor { get; set; }

        // --- RELACIÓN CON EMPRESAS AFILIADAS (Solo para Inspectores) ---

        /// <summary>
        /// Empresas afiliadas asignadas a este inspector.
        /// Relación Many-to-Many a través de InspectorAffiliatedCompany.
        /// </summary>
        public virtual ICollection<AffiliatedCompany> AssignedCompanies { get; set; } = new HashSet<AffiliatedCompany>();
    }
}

