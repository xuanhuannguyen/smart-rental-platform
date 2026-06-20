using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyMeterReadingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_meter_readings_contract_id_service_type_id_billing_period_s~",
                table: "meter_readings");

            migrationBuilder.DropColumn(
                name: "status",
                table: "meter_readings");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_contract_id_service_type_id_billing_period_s~",
                table: "meter_readings",
                columns: new[] { "contract_id", "service_type_id", "billing_period_start", "billing_period_end" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_meter_readings_contract_id_service_type_id_billing_period_s~",
                table: "meter_readings");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "meter_readings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_contract_id_service_type_id_billing_period_s~",
                table: "meter_readings",
                columns: new[] { "contract_id", "service_type_id", "billing_period_start", "billing_period_end" },
                unique: true);
        }
    }
}
