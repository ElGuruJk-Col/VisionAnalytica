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
        public DbSet<Photo> Photos { get; set; }

        // --- EMPRESAS AFILIADAS (Sistema de Roles) ---
        public DbSet<AffiliatedCompany> AffiliatedCompanies { get; set; }

        // --- REFRESH TOKENS (Sistema de Renovación de Tokens) ---
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        // --- CONFIGURACIÓN DE ORGANIZACIÓN (Optimización de Imágenes) ---
        public DbSet<OrganizationSettings> OrganizationSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // CRÍTICO: Aplica las reglas de Identity

            // ===================================
            // --- REGLAS DE NEGOCIO Y MODELADO ---
            // ===================================

            // 1. Reglas para la entidad User (Relación con Organization y Supervisor)
            builder.Entity<User>(entity =>
            {
                // Un Usuario pertenece a UNA Organización.
                entity.HasOne(u => u.Organization)
                      // Una Organización tiene MUCHOS Usuarios.
                      .WithMany(o => o.Users)
                      .HasForeignKey(u => u.OrganizationId)
                      // Si la Organización se borra, los Usuarios NO se borran (Restrict).
                      .OnDelete(DeleteBehavior.Restrict);

                // Relación de Supervisor (self-referencing)
                // Un Inspector puede tener un Supervisor (Admin/SuperAdmin)
                entity.HasOne(u => u.Supervisor)
                      .WithMany()
                      .HasForeignKey(u => u.SupervisorId)
                      .OnDelete(DeleteBehavior.NoAction); // NoAction para evitar ciclos de cascada en SQL Server
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

                // Relación 1:1 con OrganizationSettings
                entity.HasOne(o => o.Settings)
                      .WithOne(s => s.Organization)
                      .HasForeignKey<OrganizationSettings>(s => s.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade);
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

                // Relación 1:N con Photo (las fotos capturadas).
                // Usamos Restrict en lugar de Cascade para evitar múltiples rutas de cascada
                entity.HasMany(i => i.Photos)
                      .WithOne(p => p.Inspection)
                      .HasForeignKey(p => p.InspectionId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // 4. Reglas para la entidad Photo
            builder.Entity<Photo>(entity =>
            {
                // Relación N:1 con Inspection (la inspección a la que pertenece).
                // Usamos Restrict en lugar de Cascade para evitar múltiples rutas de cascada
                entity.HasOne(p => p.Inspection)
                      .WithMany(i => i.Photos)
                      .HasForeignKey(p => p.InspectionId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                // Relación 1:N con Finding (los hallazgos de esta foto).
                // Si se borra la foto, se borran sus hallazgos (Cascade).
                entity.HasMany(p => p.Findings)
                      .WithOne(f => f.Photo)
                      .HasForeignKey(f => f.PhotoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 5. Reglas para la entidad AffiliatedCompany
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

            // 6. Relación Many-to-Many: Inspector (User) ↔ Empresas Afiliadas
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

            // 7. Reglas para la entidad RefreshToken
            builder.Entity<RefreshToken>(entity =>
            {
                // Relación N:1 con User
                entity.HasOne(rt => rt.User)
                      .WithMany()
                      .HasForeignKey(rt => rt.UserId)
                      .OnDelete(DeleteBehavior.Cascade); // Si se borra el usuario, se borran sus refresh tokens

                // Índice único en el token para búsquedas rápidas
                entity.HasIndex(rt => rt.Token).IsUnique();

                // Ignorar propiedades calculadas (no se mapean a la BD)
                entity.Ignore(rt => rt.IsRevoked);
                entity.Ignore(rt => rt.IsExpired);
                entity.Ignore(rt => rt.IsActive);

                // Índice compuesto para búsquedas por usuario y expiración
                entity.HasIndex(rt => new { rt.UserId, rt.ExpiresAt });
            });

            // 8. Reglas para la entidad OrganizationSettings
            builder.Entity<OrganizationSettings>(entity =>
            {
                // Relación 1:1 con Organization
                entity.HasOne(os => os.Organization)
                      .WithOne()
                      .HasForeignKey<OrganizationSettings>(os => os.OrganizationId)
                      .OnDelete(DeleteBehavior.Cascade); // Si se borra la organización, se borra su configuración

                // Índice único en OrganizationId para garantizar una sola configuración por organización
                entity.HasIndex(os => os.OrganizationId).IsUnique();
            });
        }
    }
}