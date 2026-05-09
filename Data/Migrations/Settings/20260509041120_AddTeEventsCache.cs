using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCraftyStash.Data.Migrations.Settings
{
    /// <inheritdoc />
    public partial class AddTeEventsCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "te_events_cache",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    external_id = table.Column<string>(type: "TEXT", nullable: false),
                    event_date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    url = table.Column<string>(type: "TEXT", nullable: true),
                    image_url = table.Column<string>(type: "TEXT", nullable: true),
                    fetched_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_te_events_cache", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_te_events_cache_event_date",
                table: "te_events_cache",
                column: "event_date");

            migrationBuilder.CreateIndex(
                name: "IX_te_events_cache_external_id",
                table: "te_events_cache",
                column: "external_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "te_events_cache");
        }
    }
}
