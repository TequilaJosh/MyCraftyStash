using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCraftyStash.Data.Migrations.Settings
{
    /// <inheritdoc />
    public partial class FixColorMatchUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_color_matches_system_external_code",
                table: "color_matches");

            migrationBuilder.CreateIndex(
                name: "IX_color_matches_system_external_code",
                table: "color_matches",
                columns: new[] { "system", "external_code" });

            migrationBuilder.CreateIndex(
                name: "IX_color_matches_system_external_code_te_color_name",
                table: "color_matches",
                columns: new[] { "system", "external_code", "te_color_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_color_matches_system_external_code",
                table: "color_matches");

            migrationBuilder.DropIndex(
                name: "IX_color_matches_system_external_code_te_color_name",
                table: "color_matches");

            migrationBuilder.CreateIndex(
                name: "IX_color_matches_system_external_code",
                table: "color_matches",
                columns: new[] { "system", "external_code" },
                unique: true);
        }
    }
}
