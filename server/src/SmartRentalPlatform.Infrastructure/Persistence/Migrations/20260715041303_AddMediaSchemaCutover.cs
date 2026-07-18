using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSchemaCutover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM property_images;
                """);

            migrationBuilder.Sql("""
                DELETE FROM contract_occupant_documents;
                """);

            migrationBuilder.Sql("""
                UPDATE users
                SET avatar_url = NULL
                WHERE avatar_url LIKE '/uploads/%'
                   OR avatar_url LIKE 'uploads/%'
                   OR avatar_url LIKE '/api/media/public/%'
                   OR avatar_url LIKE 'api/media/public/%'
                   OR avatar_url LIKE 'public/%'
                   OR avatar_url LIKE 'demo/%'
                   OR avatar_url LIKE 'kfc-scenario/%'
                   OR avatar_url LIKE 'seed/%';
                """);

            migrationBuilder.DropColumn(
                name: "pdf_object_key",
                table: "rooming_house_rules");

            migrationBuilder.DropColumn(
                name: "back_image_object_key",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "extra_image_object_key",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "front_image_object_key",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "object_key",
                table: "property_images");

            migrationBuilder.DropColumn(
                name: "proof_image_object_key",
                table: "meter_readings");

            migrationBuilder.DropColumn(
                name: "back_image_object_key",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "front_image_object_key",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "selfie_image_object_key",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "evidence_file_object_key",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "signed_file_object_key",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "unsigned_file_object_key",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "back_image_object_key",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "extra_image_object_key",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "front_image_object_key",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "storage_object_key",
                table: "contract_files");

            migrationBuilder.RenameColumn(
                name: "FileUrl",
                table: "contract_files",
                newName: "file_url");

            migrationBuilder.AddColumn<Guid>(
                name: "avatar_media_asset_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                table: "rooming_house_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "back_media_asset_id",
                table: "rooming_house_legal_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "extra_media_asset_id",
                table: "rooming_house_legal_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "front_media_asset_id",
                table: "rooming_house_legal_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                table: "property_images",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "proof_media_asset_id",
                table: "meter_readings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "back_media_asset_id",
                table: "kyc_verifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "front_media_asset_id",
                table: "kyc_verifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "selfie_media_asset_id",
                table: "kyc_verifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "evidence_file_media_asset_id",
                table: "contract_signing_envelopes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "signed_file_media_asset_id",
                table: "contract_signing_envelopes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "unsigned_file_media_asset_id",
                table: "contract_signing_envelopes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "back_media_asset_id",
                table: "contract_occupant_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "extra_media_asset_id",
                table: "contract_occupant_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "front_media_asset_id",
                table: "contract_occupant_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                table: "contract_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bucket_name = table.Column<string>(type: "text", nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    original_file_name = table.Column<string>(type: "text", nullable: false),
                    stored_file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    file_hash = table.Column<string>(type: "text", nullable: true),
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Private"),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PendingUpload"),
                    linked_entity_type = table.Column<string>(type: "text", nullable: true),
                    linked_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_media_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_media_audit_logs_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_avatar_media_asset_id",
                table: "users",
                column: "avatar_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_rules_media_asset_id",
                table: "rooming_house_rules",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_legal_documents_back_media_asset_id",
                table: "rooming_house_legal_documents",
                column: "back_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_legal_documents_extra_media_asset_id",
                table: "rooming_house_legal_documents",
                column: "extra_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooming_house_legal_documents_front_media_asset_id",
                table: "rooming_house_legal_documents",
                column: "front_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_property_images_media_asset_id",
                table: "property_images",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_proof_media_asset_id",
                table: "meter_readings",
                column: "proof_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_kyc_verifications_back_media_asset_id",
                table: "kyc_verifications",
                column: "back_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_kyc_verifications_front_media_asset_id",
                table: "kyc_verifications",
                column: "front_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_kyc_verifications_selfie_media_asset_id",
                table: "kyc_verifications",
                column: "selfie_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signing_envelopes_evidence_file_media_asset_id",
                table: "contract_signing_envelopes",
                column: "evidence_file_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signing_envelopes_signed_file_media_asset_id",
                table: "contract_signing_envelopes",
                column: "signed_file_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_signing_envelopes_unsigned_file_media_asset_id",
                table: "contract_signing_envelopes",
                column: "unsigned_file_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_back_media_asset_id",
                table: "contract_occupant_documents",
                column: "back_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_extra_media_asset_id",
                table: "contract_occupant_documents",
                column: "extra_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_occupant_documents_front_media_asset_id",
                table: "contract_occupant_documents",
                column: "front_media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_media_asset_id",
                table: "contract_files",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_created_at",
                table: "media_assets",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_deleted_at",
                table: "media_assets",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_linked_entity_type_linked_entity_id",
                table: "media_assets",
                columns: new[] { "linked_entity_type", "linked_entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_object_key",
                table: "media_assets",
                column: "object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_owner_user_id",
                table: "media_assets",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_assets_scope_status",
                table: "media_assets",
                columns: new[] { "scope", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_media_audit_logs_actor_user_id",
                table: "media_audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_audit_logs_created_at",
                table: "media_audit_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_media_audit_logs_media_asset_id",
                table: "media_audit_logs",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_media_audit_logs_media_asset_id_created_at",
                table: "media_audit_logs",
                columns: new[] { "media_asset_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_contract_files_media_assets_media_asset_id",
                table: "contract_files",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_occupant_documents_media_assets_back_media_asset_id",
                table: "contract_occupant_documents",
                column: "back_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_occupant_documents_media_assets_extra_media_asset_~",
                table: "contract_occupant_documents",
                column: "extra_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_occupant_documents_media_assets_front_media_asset_~",
                table: "contract_occupant_documents",
                column: "front_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_signing_envelopes_media_assets_evidence_file_media~",
                table: "contract_signing_envelopes",
                column: "evidence_file_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_signing_envelopes_media_assets_signed_file_media_a~",
                table: "contract_signing_envelopes",
                column: "signed_file_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_contract_signing_envelopes_media_assets_unsigned_file_media~",
                table: "contract_signing_envelopes",
                column: "unsigned_file_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_kyc_verifications_media_assets_back_media_asset_id",
                table: "kyc_verifications",
                column: "back_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_kyc_verifications_media_assets_front_media_asset_id",
                table: "kyc_verifications",
                column: "front_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_kyc_verifications_media_assets_selfie_media_asset_id",
                table: "kyc_verifications",
                column: "selfie_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_meter_readings_media_assets_proof_media_asset_id",
                table: "meter_readings",
                column: "proof_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_property_images_media_assets_media_asset_id",
                table: "property_images",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_back_media_asset~",
                table: "rooming_house_legal_documents",
                column: "back_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_extra_media_asse~",
                table: "rooming_house_legal_documents",
                column: "extra_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_front_media_asse~",
                table: "rooming_house_legal_documents",
                column: "front_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_rooming_house_rules_media_assets_media_asset_id",
                table: "rooming_house_rules",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_users_media_assets_avatar_media_asset_id",
                table: "users",
                column: "avatar_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contract_files_media_assets_media_asset_id",
                table: "contract_files");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_occupant_documents_media_assets_back_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_occupant_documents_media_assets_extra_media_asset_~",
                table: "contract_occupant_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_occupant_documents_media_assets_front_media_asset_~",
                table: "contract_occupant_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_signing_envelopes_media_assets_evidence_file_media~",
                table: "contract_signing_envelopes");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_signing_envelopes_media_assets_signed_file_media_a~",
                table: "contract_signing_envelopes");

            migrationBuilder.DropForeignKey(
                name: "FK_contract_signing_envelopes_media_assets_unsigned_file_media~",
                table: "contract_signing_envelopes");

            migrationBuilder.DropForeignKey(
                name: "FK_kyc_verifications_media_assets_back_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropForeignKey(
                name: "FK_kyc_verifications_media_assets_front_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropForeignKey(
                name: "FK_kyc_verifications_media_assets_selfie_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropForeignKey(
                name: "FK_meter_readings_media_assets_proof_media_asset_id",
                table: "meter_readings");

            migrationBuilder.DropForeignKey(
                name: "FK_property_images_media_assets_media_asset_id",
                table: "property_images");

            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_back_media_asset~",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_extra_media_asse~",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_front_media_asse~",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_rules_media_assets_media_asset_id",
                table: "rooming_house_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_users_media_assets_avatar_media_asset_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "media_audit_logs");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropIndex(
                name: "IX_users_avatar_media_asset_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_rules_media_asset_id",
                table: "rooming_house_rules");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_legal_documents_back_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_legal_documents_extra_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_legal_documents_front_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropIndex(
                name: "IX_property_images_media_asset_id",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "IX_meter_readings_proof_media_asset_id",
                table: "meter_readings");

            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_back_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_front_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_selfie_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropIndex(
                name: "IX_contract_signing_envelopes_evidence_file_media_asset_id",
                table: "contract_signing_envelopes");

            migrationBuilder.DropIndex(
                name: "IX_contract_signing_envelopes_signed_file_media_asset_id",
                table: "contract_signing_envelopes");

            migrationBuilder.DropIndex(
                name: "IX_contract_signing_envelopes_unsigned_file_media_asset_id",
                table: "contract_signing_envelopes");

            migrationBuilder.DropIndex(
                name: "IX_contract_occupant_documents_back_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropIndex(
                name: "IX_contract_occupant_documents_extra_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropIndex(
                name: "IX_contract_occupant_documents_front_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropIndex(
                name: "IX_contract_files_media_asset_id",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "avatar_media_asset_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "rooming_house_rules");

            migrationBuilder.DropColumn(
                name: "back_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "extra_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "front_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "property_images");

            migrationBuilder.DropColumn(
                name: "proof_media_asset_id",
                table: "meter_readings");

            migrationBuilder.DropColumn(
                name: "back_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "front_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "selfie_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "evidence_file_media_asset_id",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "signed_file_media_asset_id",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "unsigned_file_media_asset_id",
                table: "contract_signing_envelopes");

            migrationBuilder.DropColumn(
                name: "back_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "extra_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "front_media_asset_id",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "contract_files");

            migrationBuilder.RenameColumn(
                name: "file_url",
                table: "contract_files",
                newName: "FileUrl");

            migrationBuilder.AddColumn<string>(
                name: "pdf_object_key",
                table: "rooming_house_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "back_image_object_key",
                table: "rooming_house_legal_documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "extra_image_object_key",
                table: "rooming_house_legal_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "front_image_object_key",
                table: "rooming_house_legal_documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "object_key",
                table: "property_images",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "proof_image_object_key",
                table: "meter_readings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "back_image_object_key",
                table: "kyc_verifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "front_image_object_key",
                table: "kyc_verifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "selfie_image_object_key",
                table: "kyc_verifications",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "evidence_file_object_key",
                table: "contract_signing_envelopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signed_file_object_key",
                table: "contract_signing_envelopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unsigned_file_object_key",
                table: "contract_signing_envelopes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "back_image_object_key",
                table: "contract_occupant_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extra_image_object_key",
                table: "contract_occupant_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "front_image_object_key",
                table: "contract_occupant_documents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "storage_object_key",
                table: "contract_files",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
