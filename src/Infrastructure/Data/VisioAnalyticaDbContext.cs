using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Infrastructure.Data
{
    /// <summary>
    /// Este es el "cerebro" de Entity Framework Core.
    /// Es el mapa que le dice a EF cómo nuestras clases (Modelos)
    /// se traducen en tablas de la base de datos.
    ///
    /// Hereda de 'IdentityDbContext' en lugar de 'DbContext'
    /// para que automáticamente obtengamos todas las tablas de
    /// ASP.NET Core Identity (Users, Roles, Claims, etc.)
    /// </summary>
    public class VisioAnalyticaDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        // Nuestros 'DbSet' (Tablas) personalizadas.
        // ¡Las tablas de Identity (como 'Users') se añaden automáticamente!
        public DbSet<Organization> Organizations { get; set; }

        public VisioAnalyticaDbContext(DbContextOptions<VisioAnalyticaDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // --- AQUÍ PODEMOS DEFINIR REGLAS DE NEGOCIO EN LA BBDD ---

            // Ejemplo: Asegurarnos de que la relación entre User y Organization
            // esté configurada correctamente (aunque EF suele inferirlo bien).

            builder.Entity<User>(entity =>
            {
                // Un Usuario tiene UNA Organización...
                entity.HasOne(u => u.Organization)
                      // ...una Organización tiene MUCHOS Usuarios.
                      .WithMany(o => o.Users)
                      // La clave foránea es OrganizationId.
                      .HasForeignKey(u => u.OrganizationId)
                      // No queremos borrado en cascada (regla de negocio).
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Organization>(entity =>
            {
                // Asegurarnos de que el nombre de la organización sea único
                entity.HasIndex(o => o.Name).IsUnique();
            });
        }
    }
}
