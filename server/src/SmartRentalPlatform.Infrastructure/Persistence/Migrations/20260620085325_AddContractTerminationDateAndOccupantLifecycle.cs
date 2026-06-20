using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractTerminationDateAndOccupantLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "termination_date",
                table: "contracts",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    legacy_id uuid := '60000000-0000-0000-0000-000000000003';
                    internet_id uuid;
                BEGIN
                    SELECT id INTO internet_id
                    FROM billing_service_types
                    WHERE name = 'Internet'
                    LIMIT 1;

                    IF internet_id IS NULL THEN
                        UPDATE billing_service_types
                        SET name = 'Internet'
                        WHERE id = legacy_id;
                    ELSIF internet_id <> legacy_id THEN
                        DELETE FROM rooming_house_service_prices legacy_price
                        WHERE legacy_price.service_type_id = legacy_id
                          AND EXISTS (
                              SELECT 1
                              FROM rooming_house_service_prices target_price
                              WHERE target_price.service_type_id = internet_id
                                AND target_price.rooming_house_id = legacy_price.rooming_house_id
                                AND target_price.effective_from = legacy_price.effective_from
                          );

                        UPDATE rooming_house_service_prices
                        SET service_type_id = internet_id
                        WHERE service_type_id = legacy_id;

                        DELETE FROM meter_readings legacy_reading
                        WHERE legacy_reading.service_type_id = legacy_id
                          AND EXISTS (
                              SELECT 1
                              FROM meter_readings target_reading
                              WHERE target_reading.service_type_id = internet_id
                                AND target_reading.contract_id = legacy_reading.contract_id
                                AND target_reading.billing_period_start = legacy_reading.billing_period_start
                                AND target_reading.billing_period_end = legacy_reading.billing_period_end
                          );

                        UPDATE meter_readings
                        SET service_type_id = internet_id
                        WHERE service_type_id = legacy_id;

                        UPDATE invoice_items
                        SET service_type_id = internet_id
                        WHERE service_type_id = legacy_id;

                        DELETE FROM billing_service_types
                        WHERE id = legacy_id;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "termination_date",
                table: "contracts");

            migrationBuilder.UpdateData(
                table: "billing_service_types",
                keyColumn: "id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000003"),
                column: "name",
                value: "Wifi");
        }
    }
}
