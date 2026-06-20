using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminConfigurableBillingServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_service_types_code",
                table: "billing_service_types");

            migrationBuilder.DropColumn(
                name: "unit_name",
                table: "rooming_house_service_prices");

            migrationBuilder.DropColumn(
                name: "code",
                table: "billing_service_types");

            migrationBuilder.RenameColumn(
                name: "billing_method",
                table: "rooming_house_service_prices",
                newName: "pricing_unit");

            migrationBuilder.RenameColumn(
                name: "is_metered",
                table: "billing_service_types",
                newName: "supports_meter_reading");

            migrationBuilder.AddColumn<string>(
                name: "meter_unit_name",
                table: "billing_service_types",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                columns: new[] { "meter_unit_name", "name" },
                values: new object[] { "kWh", "Điện" });

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                columns: new[] { "meter_unit_name", "name" },
                values: new object[] { "m3", "Nước" });

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "meter_unit_name",
                value: null);

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000004"),
                columns: new[] { "meter_unit_name", "name" },
                values: new object[] { null, "Rác" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_service_types_name",
                table: "billing_service_types",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_service_types_name",
                table: "billing_service_types");

            migrationBuilder.DropColumn(
                name: "meter_unit_name",
                table: "billing_service_types");

            migrationBuilder.RenameColumn(
                name: "pricing_unit",
                table: "rooming_house_service_prices",
                newName: "billing_method");

            migrationBuilder.RenameColumn(
                name: "supports_meter_reading",
                table: "billing_service_types",
                newName: "is_metered");

            migrationBuilder.AddColumn<string>(
                name: "unit_name",
                table: "rooming_house_service_prices",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "billing_service_types",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                columns: new[] { "code", "name" },
                values: new object[] { "Electric", "Electric" });

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                columns: new[] { "code", "name" },
                values: new object[] { "Water", "Water" });

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "code",
                value: "Wifi");

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000004"),
                columns: new[] { "code", "name" },
                values: new object[] { "Trash", "Trash" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_service_types_code",
                table: "billing_service_types",
                column: "code",
                unique: true);
        }
    }
}
