using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSchemaFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "media_audit_logs");

            migrationBuilder.DropTable(
                name: "media_assets");
        }
    }
}
