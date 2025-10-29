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
    }
}

