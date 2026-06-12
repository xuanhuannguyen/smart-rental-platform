using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRentalRequestContractFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lease_policies");

            migrationBuilder.CreateTable(
                name: "rental_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rooming_house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_rental_months = table.Column<int>(type: "integer", nullable: false),
                    max_rental_months = table.Column<int>(type: "integer", nullable: false),
                    allow_short_term_renewal = table.Column<bool>(type: "boolean", nullable: false),
                    renewal_notice_days = table.Column<int>(type: "integer", nullable: false),
                    deposit_months = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rental_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_rental_policies_rooming_houses_rooming_house_id",
                        column: x => x.rooming_house_id,
                        principalTable: "rooming_houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rental_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by_landlord_id = table.Column<Guid>(type: "uuid", nullable: true),
                    desired_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_occupant_count = table.Column<int>(type: "integer", nullable: false),
                    monthly_rent_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_amount_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    tenant_note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rental_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_rental_requests_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rental_requests_users_approved_by_landlord_id",
                        column: x => x.approved_by_landlord_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_rental_requests_users_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_deposits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    landlord_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "VND"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    payment_deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    refunded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    forfeited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    refund_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    forfeited_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    payment_transfer_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refund_transfer_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_deposits", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_deposits_rental_requests_rental_request_id",
                        column: x => x.rental_request_id,
                        principalTable: "rental_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_deposits_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_deposits_users_landlord_user_id",
                        column: x => x.landlord_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_room_deposits_users_tenant_user_id",
                        column: x => x.tenant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rental_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_deposit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    main_tenant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    monthly_rent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    payment_day = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    room_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    signature_deadline_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.id);
                    table.ForeignKey(
                        name: "FK_contracts_rental_requests_rental_request_id",
                        column: x => x.rental_request_id,
                        principalTable: "rental_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contracts_room_deposits_room_deposit_id",
                        column: x => x.room_deposit_id,
                        principalTable: "room_deposits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "contract_appendices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appendix_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_appendices", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_appendices_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_appendices_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_occupants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guardian_occupant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    relationship_to_main_tenant = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    move_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                    move_out_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_occupants", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_occupants_contract_occupants_guardian_occupant_id",
                        column: x => x.guardian_occupant_id,
                        principalTable: "contract_occupants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contract_occupants_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_occupants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_appendix_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    appendix_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    field_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    old_value = table.Column<string>(type: "jsonb", nullable: true),
                    new_value = table.Column<string>(type: "jsonb", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_appendix_changes", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_appendix_changes_contract_appendices_appendix_id",
                        column: x => x.appendix_id,
                        principalTable: "contract_appendices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_files",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appendix_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_object_key = table.Column<string>(type: "text", nullable: false),
                    file_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_files_contract_appendices_appendix_id",
                        column: x => x.appendix_id,
                        principalTable: "contract_appendices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_files_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contract_signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    appendix_id = table.Column<Guid>(type: "uuid", nullable: true),
                    signer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signer_role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    signature_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    signature_text = table.Column<string>(type: "text", nullable: true),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_signatures", x => x.id);
                    table.CheckConstraint("ck_contract_signatures_target_exclusive", "(contract_id IS NOT NULL AND appendix_id IS NULL) OR (contract_id IS NULL AND appendix_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_contract_signatures_contract_appendices_appendix_id",
                        column: x => x.appendix_id,
                        principalTable: "contract_appendices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_signatures_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contract_signatures_users_signer_user_id",
                        column: x => x.signer_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contract_occupant_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_occupant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    document_number_masked = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    document_number_hash = table.Column<string>(type: "text", nullable: true),
                    front_image_object_key = table.Column<string>(type: "text", nullable: false),
                    back_image_object_key = table.Column<string>(type: "text", nullable: true),
                    extra_image_object_key = table.Column<string>(type: "text", nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_occupant_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_occupant_documents_contract_occupants_contract_occ~",
                        column: x => x.contract_occupant_id,
                        principalTable: "contract_occupants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contract_appendices_contract_id_appendix_number",
                table: "contract_appendices",
                columns: new[] { "contract_id", "appendix_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_appendices_created_by_user_id",
                table: "contract_appendices",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_appendices_status",
                table: "contract_appendices",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_contract_appendix_changes_appendix_id",
                table: "contract_appendix_changes",
                column: "appendix_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_appendix_changes_appendix_id_sort_order",
                table: "contract_appendix_changes",
                columns: new[] { "appendix_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_appendix_id",
                table: "contract_files",
                column: "appendix_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_contract_id",
                table: "contract_files",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_contract_occupant_id",
                table: "contract_occupant_documents",
                column: "contract_occupant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_document_number_hash",
                table: "contract_occupant_documents",
                column: "document_number_hash");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupants_contract_id",
                table: "contract_occupants",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupants_guardian_occupant_id",
                table: "contract_occupants",
                column: "guardian_occupant_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupants_status",
                table: "contract_occupants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupants_user_id",
                table: "contract_occupants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signatures_appendix_id",
                table: "contract_signatures",
                column: "appendix_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signatures_contract_id",
                table: "contract_signatures",
                column: "contract_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signatures_signer_user_id",
                table: "contract_signatures",
                column: "signer_user_id");

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
                name: "IX_contracts_rental_request_id",
                table: "contracts",
                column: "rental_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_room_deposit_id",
                table: "contracts",
                column: "room_deposit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contracts_room_id",
                table: "contracts",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_contracts_status",
                table: "contracts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_rental_policies_rooming_house_id",
                table: "rental_policies",
                column: "rooming_house_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rental_requests_approved_by_landlord_id",
                table: "rental_requests",
                column: "approved_by_landlord_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_requests_room_id",
                table: "rental_requests",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_rental_requests_status",
                table: "rental_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_rental_requests_tenant_user_id",
                table: "rental_requests",
                column: "tenant_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_deposits_landlord_user_id",
                table: "room_deposits",
                column: "landlord_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_deposits_rental_request_id",
                table: "room_deposits",
                column: "rental_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_deposits_room_id",
                table: "room_deposits",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_deposits_status",
                table: "room_deposits",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_room_deposits_tenant_user_id",
                table: "room_deposits",
                column: "tenant_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contract_appendix_changes");

            migrationBuilder.DropTable(
                name: "contract_files");

            migrationBuilder.DropTable(
                name: "contract_occupant_documents");

            migrationBuilder.DropTable(
                name: "contract_signatures");

            migrationBuilder.DropTable(
                name: "rental_policies");

            migrationBuilder.DropTable(
                name: "contract_occupants");

            migrationBuilder.DropTable(
                name: "contract_appendices");

            migrationBuilder.DropTable(
                name: "contracts");

            migrationBuilder.DropTable(
                name: "room_deposits");

            migrationBuilder.DropTable(
                name: "rental_requests");

            migrationBuilder.CreateTable(
                name: "lease_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rooming_house_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allow_short_term_renewal = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deposit_months = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_12_months_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_24_months_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_6_months_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    discount_9_months_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    renewal_notice_days = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lease_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_lease_policies_rooming_houses_rooming_house_id",
                        column: x => x.rooming_house_id,
                        principalTable: "rooming_houses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lease_policies_rooming_house_id",
                table: "lease_policies",
                column: "rooming_house_id",
                unique: true);
        }
    }
}
