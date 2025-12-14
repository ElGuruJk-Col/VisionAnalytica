using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisioAnalytica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePhotoFindingRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Findings_Inspections_InspectionId",
                table: "Findings");

            migrationBuilder.DropForeignKey(
                name: "FK_Photos_Inspections_AnalysisInspectionId",
                table: "Photos");

            migrationBuilder.DropIndex(
                name: "IX_Photos_AnalysisInspectionId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "AnalysisInspectionId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Photos");

            migrationBuilder.RenameColumn(
                name: "InspectionId",
                table: "Findings",
                newName: "PhotoId");

            migrationBuilder.RenameIndex(
                name: "IX_Findings_InspectionId",
                table: "Findings",
                newName: "IX_Findings_PhotoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Findings_Photos_PhotoId",
                table: "Findings",
                column: "PhotoId",
                principalTable: "Photos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Findings_Photos_PhotoId",
                table: "Findings");

            migrationBuilder.RenameColumn(
                name: "PhotoId",
                table: "Findings",
                newName: "InspectionId");

            migrationBuilder.RenameIndex(
                name: "IX_Findings_PhotoId",
                table: "Findings",
                newName: "IX_Findings_InspectionId");

            migrationBuilder.AddColumn<Guid>(
                name: "AnalysisInspectionId",
                table: "Photos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Photos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Photos_AnalysisInspectionId",
                table: "Photos",
                column: "AnalysisInspectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Findings_Inspections_InspectionId",
                table: "Findings",
                column: "InspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_Inspections_AnalysisInspectionId",
                table: "Photos",
                column: "AnalysisInspectionId",
                principalTable: "Inspections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
