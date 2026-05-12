using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCraftyStash.Data.Migrations.Settings
{
    /// <inheritdoc />
    public partial class AddTeDailyCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "te_daily_calendar",
                columns: table => new
                {
                    date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    hours = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: true),
                    events_json = table.Column<string>(type: "TEXT", nullable: true),
                    fetched_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_te_daily_calendar", x => x.date);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "te_daily_calendar");
        }
    }
}
