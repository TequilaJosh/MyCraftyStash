using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCraftyStash.Data.Migrations.Settings
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "config_lists",
                columns: table => new
                {
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_lists", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "custom_colors",
                columns: table => new
                {
                    brush_key = table.Column<string>(type: "TEXT", nullable: false),
                    hex = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_colors", x => x.brush_key);
                });

            migrationBuilder.CreateTable(
                name: "kv_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kv_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "type_sort_orders",
                columns: table => new
                {
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    sort1 = table.Column<string>(type: "TEXT", nullable: false),
                    sort2 = table.Column<string>(type: "TEXT", nullable: false),
                    sort3 = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_type_sort_orders", x => x.type);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "config_lists");

            migrationBuilder.DropTable(
                name: "custom_colors");

            migrationBuilder.DropTable(
                name: "kv_settings");

            migrationBuilder.DropTable(
                name: "type_sort_orders");
        }
    }
}
