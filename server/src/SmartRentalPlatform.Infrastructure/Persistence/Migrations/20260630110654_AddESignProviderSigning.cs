using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddESignProviderSigning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contract_files_contract_id_appendix_id_file_variant",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "signature_text",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "file_variant",
                table: "contract_files");

            migrationBuilder.RenameColumn(
                name: "file_url",
                table: "contract_files",
                newName: "FileUrl");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "signed_at",
                table: "contract_signatures",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "certificate_issuer",
                table: "contract_signatures",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_serial_number",
                table: "contract_signatures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certificate_subject",
                table: "contract_signatures",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "notified_at",
                table: "contract_signatures",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "contract_signatures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_envelope_id",
                table: "contract_signatures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_evidence_json",
                table: "contract_signatures",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider_participant_id",
                table: "contract_signatures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signed_file_sha256_hash",
                table: "contract_signatures",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "signing_envelope_id",
                table: "contract_signatures",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "signing_order",
                table: "contract_signatures",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "signing_url",
                table: "contract_signatures",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "contract_signatures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Signed");

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "contract_files",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_legally_signed",
                table: "contract_files",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "contract_files",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Preview");

            migrationBuilder.AddColumn<string>(
                name: "sha256_hash",
                table: "contract_files",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "signing_envelope_id",
                table: "contract_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE contract_signatures
                SET signing_order = CASE signer_role
                        WHEN 'Landlord' THEN 1
                        WHEN 'Tenant' THEN 2
                        ELSE signing_order
                    END,
                    status = CASE
                        WHEN signed_at IS NULL THEN 'Pending'
                        ELSE 'Signed'
                    END;
                """);

            migrationBuilder.CreateTable(
                name: "contract_signing_envelopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    appendix_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_envelope_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    unsigned_file_object_key = table.Column<string>(type: "text", nullable: false),
                    unsigned_file_sha256_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signed_file_object_key = table.Column<string>(type: "text", nullable: true),
                    signed_file_sha256_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    evidence_file_object_key = table.Column<string>(type: "text", nullable: true),
                    provider_status_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_signing_envelopes", x => x.id);
                    table.ForeignKey(
                        name: "FK_contract_signing_envelopes_contract_appendices_appendix_id",
                        column: x => x.appendix_id,
                        principalTable: "contract_appendices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_contract_signing_envelopes_contracts_contract_id",
                        column: x => x.contract_id,
                        principalTable: "contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "esign_webhook_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    signing_envelope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider_event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_envelope_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    raw_payload = table.Column<string>(type: "text", nullable: false),
                    raw_payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signature_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    processing_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_esign_webhook_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contract_signatures_signing_envelope_id",
                table: "contract_signatures",
                column: "signing_envelope_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_contract_id_appendix_id_purpose",
                table: "contract_files",
                columns: new[] { "contract_id", "appendix_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_sha256_hash",
                table: "contract_files",
                column: "sha256_hash");

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_signing_envelope_id",
                table: "contract_files",
                column: "signing_envelope_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signing_envelopes_appendix_id_status",
                table: "contract_signing_envelopes",
                columns: new[] { "appendix_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_signing_envelopes_contract_id_status",
                table: "contract_signing_envelopes",
                columns: new[] { "contract_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_signing_envelopes_provider_provider_envelope_id",
                table: "contract_signing_envelopes",
                columns: new[] { "provider", "provider_envelope_id" },
                unique: true,
                filter: "provider_envelope_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_esign_webhook_logs_idempotency_key",
                table: "esign_webhook_logs",
                column: "idempotency_key");

            migrationBuilder.CreateIndex(
                name: "IX_esign_webhook_logs_provider_provider_envelope_id",
                table: "esign_webhook_logs",
                columns: new[] { "provider", "provider_envelope_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_contract_files_contract_signing_envelopes_signing_envelope_~",
                table: "contract_files",
                column: "signing_envelope_id",
                principalTable: "contract_signing_envelopes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_signatures_contract_signing_envelopes_signing_enve~",
                table: "contract_signatures",
                column: "signing_envelope_id",
                principalTable: "contract_signing_envelopes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contract_files_contract_signing_envelopes_signing_envelope_~",
                table: "contract_files");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_signatures_contract_signing_envelopes_signing_enve~",
                table: "contract_signatures");

            migrationBuilder.DropTable(
                name: "contract_signing_envelopes");

            migrationBuilder.DropTable(
                name: "esign_webhook_logs");

            migrationBuilder.DropIndex(
                name: "IX_contract_signatures_signing_envelope_id",
                table: "contract_signatures");

            migrationBuilder.DropIndex(
                name: "IX_contract_files_contract_id_appendix_id_purpose",
                table: "contract_files");

            migrationBuilder.DropIndex(
                name: "IX_contract_files_sha256_hash",
                table: "contract_files");

            migrationBuilder.DropIndex(
                name: "IX_contract_files_signing_envelope_id",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "certificate_issuer",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "certificate_serial_number",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "certificate_subject",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "notified_at",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "provider_envelope_id",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "provider_evidence_json",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "provider_participant_id",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "signed_file_sha256_hash",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "signing_envelope_id",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "signing_order",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "signing_url",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "status",
                table: "contract_signatures");

            migrationBuilder.DropColumn(
                name: "content_type",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "is_legally_signed",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "sha256_hash",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "signing_envelope_id",
                table: "contract_files");

            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "contract_files",
                newName: "file_url");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "signed_at",
                table: "contract_signatures",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_text",
                table: "contract_signatures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_variant",
                table: "contract_files",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Raw");

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_contract_id_appendix_id_file_variant",
                table: "contract_files",
                columns: new[] { "contract_id", "appendix_id", "file_variant" });
        }
    }
}
