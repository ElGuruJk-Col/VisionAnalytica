using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisioAnalytica.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveImageUrlFromInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar la columna ImageUrl de la tabla Inspections
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Inspections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir: Restaurar la columna ImageUrl
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Inspections",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: string.Empty);
        }
    }
}
