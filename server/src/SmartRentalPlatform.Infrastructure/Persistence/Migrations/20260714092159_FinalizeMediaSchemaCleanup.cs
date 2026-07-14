using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeMediaSchemaCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove stale legacy property image rows and re-link the surviving media assets.
            migrationBuilder.Sql(
                """
                DELETE FROM property_images pi
                WHERE pi.media_asset_id IS NULL
                   OR NOT EXISTS (
                        SELECT 1
                        FROM media_assets ma
                        WHERE ma.id = pi.media_asset_id
                          AND ma.visibility = 'Public'
                          AND ma.scope IN ('RoomingHouseImage', 'RoomImage')
                          AND ma.status NOT IN ('PendingUpload', 'Deleted')
                   );
                """);

            migrationBuilder.Sql(
                """
                UPDATE media_assets AS ma
                SET linked_entity_type = 'PropertyImage',
                    linked_entity_id = pi.id,
                    visibility = 'Public',
                    deleted_at = NULL,
                    status = 'Linked',
                    updated_at = now()
                FROM property_images pi
                WHERE pi.media_asset_id = ma.id;
                """);

            // Rewrite public-facing image URLs to the canonical media endpoint.
            migrationBuilder.Sql(
                """
                UPDATE property_images
                SET image_url = '/api/media/public/' || media_asset_id::text
                WHERE media_asset_id IS NOT NULL;
                """);

            // Keep only valid public avatar media links and null out leftover legacy avatar URLs.
            migrationBuilder.Sql(
                """
                UPDATE users u
                SET avatar_media_asset_id = NULL
                WHERE u.avatar_media_asset_id IS NOT NULL
                  AND NOT EXISTS (
                        SELECT 1
                        FROM media_assets ma
                        WHERE ma.id = u.avatar_media_asset_id
                          AND ma.scope = 'Avatar'
                          AND ma.visibility = 'Public'
                          AND ma.status NOT IN ('PendingUpload', 'Deleted')
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE media_assets AS ma
                SET owner_user_id = u.id,
                    linked_entity_type = 'User',
                    linked_entity_id = u.id,
                    scope = 'Avatar',
                    visibility = 'Public',
                    deleted_at = NULL,
                    status = 'Linked',
                    updated_at = now()
                FROM users u
                WHERE u.avatar_media_asset_id = ma.id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE users
                SET avatar_url = NULL
                WHERE avatar_media_asset_id IS NOT NULL
                   OR (
                        avatar_url IS NOT NULL
                        AND avatar_url !~* '^https?://'
                   );
                """);

            // Remove schema compatibility columns that are now fully replaced by media assets.
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
                name: "back_image_object_key",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "extra_image_object_key",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "front_image_object_key",
                table: "contract_occupant_documents");

            migrationBuilder.DropColumn(
                name: "file_url",
                table: "contract_files");

            migrationBuilder.DropColumn(
                name: "storage_object_key",
                table: "contract_files");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "file_url",
                table: "contract_files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "storage_object_key",
                table: "contract_files",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
