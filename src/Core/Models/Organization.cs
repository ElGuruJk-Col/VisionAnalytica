using System.Collections.Generic;
using System;

namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Define la entidad principal de un 'Tenant' (Cliente).
    /// Cada Organización (ej. "SURA", "Ecopetrol") es un inquilino
    /// aislado en nuestro sistema SaaS.
    /// </summary>
    public class Organization
    {
        /// <summary>
        /// Clave primaria. Usamos Guid para asegurar unicidad global
        /// y evitar que un cliente pueda 'adivinar' los IDs de otro.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Nombre público de la organización (ej. "SURA Colombia").
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Propiedad de navegación (EF Core).
        /// Una organización tiene una colección de usuarios.
        /// </summary>
        public virtual ICollection<User> Users { get; set; }

        /// <summary>
        /// Propiedad de navegación a la configuración de la organización.
        /// Relación 1:1 con OrganizationSettings.
        /// </summary>
        public virtual OrganizationSettings? Settings { get; set; }

        public Organization()
        {
            // Buena práctica: Inicializar las colecciones en el constructor
            // para evitar NullReferenceException.
            Users = new HashSet<User>();
        }
    }
}
