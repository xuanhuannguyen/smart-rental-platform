using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterReadingOcrAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ocr_raw_text",
                table: "meter_readings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ocr_reading",
                table: "meter_readings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "was_manually_corrected",
                table: "meter_readings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ocr_raw_text",
                table: "meter_readings");

            migrationBuilder.DropColumn(
                name: "ocr_reading",
                table: "meter_readings");

            migrationBuilder.DropColumn(
                name: "was_manually_corrected",
                table: "meter_readings");
        }
    }
}
