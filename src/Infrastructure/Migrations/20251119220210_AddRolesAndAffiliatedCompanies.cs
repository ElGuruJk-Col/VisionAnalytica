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
            migrationBuilder.AddColumn<Guid>(
                name: "AffiliatedCompanyId",
                table: "Inspections",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
                defaultValue: "");

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
                defaultValue: false);

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
                name: "IX_Inspections_AffiliatedCompanyId",
                table: "Inspections",
                column: "AffiliatedCompanyId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Inspections_AffiliatedCompanies_AffiliatedCompanyId",
                table: "Inspections",
                column: "AffiliatedCompanyId",
                principalTable: "AffiliatedCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
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
