using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCraftyStash.Data.Migrations.Settings
{
    /// <inheritdoc />
    public partial class AddColorMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "color_matches",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    system = table.Column<string>(type: "TEXT", nullable: false),
                    external_code = table.Column<string>(type: "TEXT", nullable: false),
                    te_color_name = table.Column<string>(type: "TEXT", nullable: false),
                    external_hex = table.Column<string>(type: "TEXT", nullable: true),
                    te_color_hex = table.Column<string>(type: "TEXT", nullable: true),
                    notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_color_matches", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_color_matches_system_external_code",
                table: "color_matches",
                columns: new[] { "system", "external_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_color_matches_system_te_color_name",
                table: "color_matches",
                columns: new[] { "system", "te_color_name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "color_matches");
        }
    }
}
