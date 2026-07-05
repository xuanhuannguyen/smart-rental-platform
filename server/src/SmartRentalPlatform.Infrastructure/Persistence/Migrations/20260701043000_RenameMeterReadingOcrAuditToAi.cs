using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameMeterReadingOcrAuditToAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ocr_raw_text",
                table: "meter_readings",
                newName: "ai_raw_text");

            migrationBuilder.RenameColumn(
                name: "ocr_reading",
                table: "meter_readings",
                newName: "ai_reading");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ai_raw_text",
                table: "meter_readings",
                newName: "ocr_raw_text");

            migrationBuilder.RenameColumn(
                name: "ai_reading",
                table: "meter_readings",
                newName: "ocr_reading");
        }
    }
}
