using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyCraftyStash.Data.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class AddProjectSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_shared",
                table: "projects",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "shared_at",
                table: "projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shared_from_name",
                table: "projects",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_shared",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "shared_at",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "shared_from_name",
                table: "projects");
        }
    }
}
