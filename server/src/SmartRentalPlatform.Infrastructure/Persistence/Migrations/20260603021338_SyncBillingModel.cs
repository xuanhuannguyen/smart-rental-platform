using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncBillingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_service_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_metered = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_service_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_deposit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    main_tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    monthly_rent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_day = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    room_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contracts_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contracts_users_main_tenant_user_id",
                        column: x => x.main_tenant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rooming_house_service_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rooming_house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooming_house_service_prices", x => x.id);
                    table.ForeignKey(
                        name: "FK_rooming_house_service_prices_billing_service_types_service_~",
                        column: x => x.service_type_id,
                        principalTable: "billing_service_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rooming_house_service_prices_rooming_houses_rooming_house_id",
                        column: x => x.rooming_house_id,
                        principalTable: "rooming_houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    landlord_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    billing_period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    billing_period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    rent_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    utility_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    service_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    remaining_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    note = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_users_landlord_user_id",
                        column: x => x.landlord_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoices_users_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meter_readings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    billing_period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    previous_reading = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    current_reading = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    consumption = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    proof_image_object_key = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    recorded_by_landlord_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reading_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_readings", x => x.id);
                    table.ForeignKey(
                        name: "FK_meter_readings_billing_service_types_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "billing_service_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meter_readings_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meter_readings_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_meter_readings_users_recorded_by_landlord_user_id",
                        column: x => x.recorded_by_landlord_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    landlord_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    wallet_transfer_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Succeeded"),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_payments_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_payments_users_landlord_user_id",
                        column: x => x.landlord_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_payments_users_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    meter_reading_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoice_items_billing_service_types_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "billing_service_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invoice_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invoice_items_meter_readings_meter_reading_id",
                        column: x => x.meter_reading_id,
                        principalTable: "meter_readings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "billing_service_types",
                columns: new[] { "id", "code", "created_at", "is_active", "is_metered", "name" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000001"), "Electric", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, true, "Electric" },
                    { new Guid("60000000-0000-0000-0000-000000000002"), "Water", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, true, "Water" },
                    { new Guid("60000000-0000-0000-0000-000000000003"), "Wifi", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, "Wifi" },
                    { new Guid("60000000-0000-0000-0000-000000000004"), "Trash", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, "Trash" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_service_types_code",
                table: "billing_service_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_contract_number",
                table: "contracts",
                column: "contract_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_main_tenant_user_id",
                table: "contracts",
                column: "main_tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_room_id_status",
                table: "contracts",
                columns: new[] { "room_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_invoice_id",
                table: "invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_meter_reading_id",
                table: "invoice_items",
                column: "meter_reading_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_items_service_type_id",
                table: "invoice_items",
                column: "service_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_invoice_id",
                table: "invoice_payments",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_landlord_user_id",
                table: "invoice_payments",
                column: "landlord_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_tenant_user_id",
                table: "invoice_payments",
                column: "tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_payments_wallet_transfer_group_id",
                table: "invoice_payments",
                column: "wallet_transfer_group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_contract_id_billing_period_start_billing_period_end",
                table: "invoices",
                columns: new[] { "contract_id", "billing_period_start", "billing_period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_invoice_no",
                table: "invoices",
                column: "invoice_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_landlord_user_id",
                table: "invoices",
                column: "landlord_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_room_id",
                table: "invoices",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_user_id",
                table: "invoices",
                column: "tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_contract_id_service_type_id_billing_period_s~",
                table: "meter_readings",
                columns: new[] { "contract_id", "service_type_id", "billing_period_start", "billing_period_end" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_recorded_by_landlord_user_id",
                table: "meter_readings",
                column: "recorded_by_landlord_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_room_id",
                table: "meter_readings",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_service_type_id",
                table: "meter_readings",
                column: "service_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_service_prices_rooming_house_id_service_type_~",
                table: "rooming_house_service_prices",
                columns: new[] { "rooming_house_id", "service_type_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_service_prices_rooming_house_id_service_type~1",
                table: "rooming_house_service_prices",
                columns: new[] { "rooming_house_id", "service_type_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_service_prices_service_type_id",
                table: "rooming_house_service_prices",
                column: "service_type_id");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_items");

            migrationBuilder.DropTable(
                name: "invoice_payments");

            migrationBuilder.DropTable(
                name: "rooming_house_service_prices");

            migrationBuilder.DropTable(
                name: "meter_readings");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "billing_service_types");

            migrationBuilder.DropTable(
                name: "contracts");
        }
    }
}
