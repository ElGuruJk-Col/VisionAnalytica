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
    public class VisioAnalyticaDbContext(DbContextOptions<VisioAnalyticaDbContext> options) : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
    {
        // Tablas base de la arquitectura.
        // FIX PARA MOQ: Usamos 'virtual' para permitir que Moq pueda simular esta propiedad.
        public virtual DbSet<Organization> Organizations { get; set; } // << ¡CORRECCIÓN CLAVE!

        // --- ¡NUEVOS DBSETS PARA PERSISTENCIA DE ANÁLISIS (Capítulo 3)! ---
        public DbSet<Inspection> Inspections { get; set; }
        public DbSet<Finding> Findings { get; set; }

        // --- EMPRESAS AFILIADAS (Sistema de Roles) ---
        public DbSet<AffiliatedCompany> AffiliatedCompanies { get; set; }

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

                // Relación N:1 con AffiliatedCompany (la empresa auditada).
                entity.HasOne(i => i.AffiliatedCompany)
                      .WithMany(ac => ac.Inspections)
                      .HasForeignKey(i => i.AffiliatedCompanyId)
                      .OnDelete(DeleteBehavior.Restrict); // Mantenemos las inspecciones aunque la empresa se desactive.

                // Relación 1:N con Finding (los hallazgos).
                entity.HasMany(i => i.Findings)
                      .WithOne(f => f.Inspection)
                      .HasForeignKey(f => f.InspectionId)
                      // ¡CRÍTICO! Si se borra la Inspección, se borran sus detalles (Cascade).
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 4. Reglas para la entidad AffiliatedCompany
            builder.Entity<AffiliatedCompany>(entity =>
            {
                // Relación N:1 con Organization.
                entity.HasOne(ac => ac.Organization)
                      .WithMany()
                      .HasForeignKey(ac => ac.OrganizationId)
                      .OnDelete(DeleteBehavior.Restrict); // No borrar empresas si se borra la organización.

                // Índice único por nombre dentro de la misma organización.
                entity.HasIndex(ac => new { ac.Name, ac.OrganizationId }).IsUnique();
            });

            // 5. Relación Many-to-Many: Inspector (User) ↔ Empresas Afiliadas
            builder.Entity<User>()
                .HasMany(u => u.AssignedCompanies)
                .WithMany(ac => ac.AssignedInspectors)
                .UsingEntity<Dictionary<string, object>>(
                    "InspectorAffiliatedCompany",
                    j => j.HasOne<AffiliatedCompany>()
                          .WithMany()
                          .HasForeignKey("AffiliatedCompanyId")
                          .OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<User>()
                          .WithMany()
                          .HasForeignKey("UserId")
                          .OnDelete(DeleteBehavior.Cascade),
                    j =>
                    {
                        j.HasKey("UserId", "AffiliatedCompanyId");
                        j.ToTable("InspectorAffiliatedCompanies");
                    });
        }
    }
}