
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Infrastructure.Data
{
    /// <summary>
    /// Este es el "cerebro" de Entity Framework Core, heredando de IdentityDbContext
    /// para integrar las tablas de autenticación de ASP.NET Core Identity.
    /// </summary>
    public class VisioAnalyticaDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        // Tablas base de la arquitectura.
        public DbSet<Organization> Organizations { get; set; }

        // --- ¡NUEVOS DBSETS PARA PERSISTENCIA DE ANÁLISIS (Capítulo 3)! ---
        public DbSet<Inspection> Inspections { get; set; }
        public DbSet<Finding> Findings { get; set; }
        // ------------------------------------------------------------------

        public VisioAnalyticaDbContext(DbContextOptions<VisioAnalyticaDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // CRÍTICO: Aplica las reglas de Identity

            // ===================================
            // --- REGLAS DE NEGOCIO Y MODELADO ---
            // ===================================

            // 1. Reglas para la entidad User (Relación con Organization)
            builder.Entity<User>(entity =>
            {
                // Un Usuario pertenece a UNA Organización.
                entity.HasOne(u => u.Organization)
                      // Una Organización tiene MUCHOS Usuarios.
                      .WithMany(o => o.Users)
                      .HasForeignKey(u => u.OrganizationId)
                      // Si la Organización se borra, los Usuarios NO se borran (Restrict).
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 2. Reglas para la entidad Organization
            builder.Entity<Organization>(entity =>
            {
                // El nombre de la organización debe ser único.
                entity.HasIndex(o => o.Name).IsUnique();

                // Relación 1:N con Inspection (para consultas Multi-Tenant).
                entity.HasMany<Inspection>()
                      .WithOne(i => i.Organization)
                      .HasForeignKey(i => i.OrganizationId)
                      .OnDelete(DeleteBehavior.Restrict); // Las Inspecciones NO se borran si la Org se borra.
            });

            // 3. Reglas para la entidad Inspection
            builder.Entity<Inspection>(entity =>
            {
                // Relación N:1 con User (el inspector).
                entity.HasOne(i => i.User)
                      .WithMany()
                      .HasForeignKey(i => i.UserId)
                      .OnDelete(DeleteBehavior.Restrict); // Mantenemos la bitácora aunque el User se borre.

                // Relación 1:N con Finding (los hallazgos).
                entity.HasMany(i => i.Findings)
                      .WithOne(f => f.Inspection)
                      .HasForeignKey(f => f.InspectionId)
                      // ¡CRÍTICO! Si se borra la Inspección, se borran sus detalles (Cascade).
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Nota: No se requiere un bloque explícito para Finding, su relación N:1
            // se define desde el lado de Inspection (su padre).
        }
    }
}