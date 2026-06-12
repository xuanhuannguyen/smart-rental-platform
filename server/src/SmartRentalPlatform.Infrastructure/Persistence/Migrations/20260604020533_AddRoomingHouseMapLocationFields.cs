using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomingHouseMapLocationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "google_map_url",
                table: "rooming_houses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "map_preview_area_url",
                table: "rooming_houses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "map_preview_generated_at",
                table: "rooming_houses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "map_preview_near_url",
                table: "rooming_houses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "map_preview_status",
                table: "rooming_houses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "map_preview_wide_url",
                table: "rooming_houses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "google_map_url",
                table: "rooming_houses");

            migrationBuilder.DropColumn(
                name: "map_preview_area_url",
                table: "rooming_houses");

            migrationBuilder.DropColumn(
                name: "map_preview_generated_at",
                table: "rooming_houses");

            migrationBuilder.DropColumn(
                name: "map_preview_near_url",
                table: "rooming_houses");

            migrationBuilder.DropColumn(
                name: "map_preview_status",
                table: "rooming_houses");

            migrationBuilder.DropColumn(
                name: "map_preview_wide_url",
                table: "rooming_houses");
        }
    }
}
