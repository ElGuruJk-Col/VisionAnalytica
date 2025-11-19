using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisioAnalytica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndAffiliatedCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- PASO 1: Agregar columnas a AspNetUsers (sin dependencias) ---
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedBy",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true); // Cambiar default a true para usuarios existentes

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            // --- PASO 2: Crear tabla AffiliatedCompanies PRIMERO ---
            migrationBuilder.CreateTable(
                name: "AffiliatedCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TaxId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AffiliatedCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AffiliatedCompanies_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectorAffiliatedCompanies",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AffiliatedCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectorAffiliatedCompanies", x => new { x.UserId, x.AffiliatedCompanyId });
                    table.ForeignKey(
                        name: "FK_InspectorAffiliatedCompanies_AffiliatedCompanies_AffiliatedCompanyId",
                        column: x => x.AffiliatedCompanyId,
                        principalTable: "AffiliatedCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspectorAffiliatedCompanies_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AffiliatedCompanies_Name_OrganizationId",
                table: "AffiliatedCompanies",
                columns: new[] { "Name", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AffiliatedCompanies_OrganizationId",
                table: "AffiliatedCompanies",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectorAffiliatedCompanies_AffiliatedCompanyId",
                table: "InspectorAffiliatedCompanies",
                column: "AffiliatedCompanyId");

            // --- PASO 3: Agregar columnas a Inspections (después de crear AffiliatedCompanies) ---
            // Primero agregamos AffiliatedCompanyId como nullable para manejar datos existentes
            migrationBuilder.AddColumn<Guid>(
                name: "AffiliatedCompanyId",
                table: "Inspections",
                type: "uniqueidentifier",
                nullable: true); // Temporalmente nullable

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Inspections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Inspections",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Inspections",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Draft");

            // --- PASO 4: Crear índice y foreign key para AffiliatedCompanyId ---
            migrationBuilder.CreateIndex(
                name: "IX_Inspections_AffiliatedCompanyId",
                table: "Inspections",
                column: "AffiliatedCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inspections_AffiliatedCompanies_AffiliatedCompanyId",
                table: "Inspections",
                column: "AffiliatedCompanyId",
                principalTable: "AffiliatedCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // --- PASO 5: Si hay inspecciones existentes, crear empresa afiliada por defecto para cada organización ---
            // Esto maneja el caso donde ya hay datos en la tabla Inspections
            migrationBuilder.Sql(@"
                -- Crear una empresa afiliada por defecto para cada organización que tenga inspecciones
                INSERT INTO AffiliatedCompanies (Id, Name, OrganizationId, IsActive, CreatedAt)
                SELECT 
                    NEWID() as Id,
                    'Empresa por Defecto - ' + o.Name as Name,
                    o.Id as OrganizationId,
                    1 as IsActive,
                    GETUTCDATE() as CreatedAt
                FROM Organizations o
                WHERE EXISTS (
                    SELECT 1 FROM Inspections i WHERE i.OrganizationId = o.Id
                )
                AND NOT EXISTS (
                    SELECT 1 FROM AffiliatedCompanies ac WHERE ac.OrganizationId = o.Id AND ac.Name LIKE 'Empresa por Defecto%'
                );
            ");

            // --- PASO 6: Asignar empresa afiliada por defecto a inspecciones existentes ---
            migrationBuilder.Sql(@"
                -- Asignar la empresa afiliada por defecto a las inspecciones existentes
                UPDATE i
                SET i.AffiliatedCompanyId = ac.Id
                FROM Inspections i
                INNER JOIN Organizations o ON i.OrganizationId = o.Id
                INNER JOIN AffiliatedCompanies ac ON ac.OrganizationId = o.Id AND ac.Name LIKE 'Empresa por Defecto%'
                WHERE i.AffiliatedCompanyId IS NULL;
            ");

            // --- PASO 7: Hacer AffiliatedCompanyId NOT NULL ahora que todos los registros tienen valor ---
            migrationBuilder.AlterColumn<Guid>(
                name: "AffiliatedCompanyId",
                table: "Inspections",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inspections_AffiliatedCompanies_AffiliatedCompanyId",
                table: "Inspections");

            migrationBuilder.DropTable(
                name: "InspectorAffiliatedCompanies");

            migrationBuilder.DropTable(
                name: "AffiliatedCompanies");

            migrationBuilder.DropIndex(
                name: "IX_Inspections_AffiliatedCompanyId",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "AffiliatedCompanyId",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Inspections");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "AspNetUsers");
        }
    }
}
