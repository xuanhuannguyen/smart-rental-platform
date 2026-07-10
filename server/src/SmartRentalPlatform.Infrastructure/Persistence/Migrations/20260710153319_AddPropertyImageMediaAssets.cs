using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyImageMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                table: "property_images",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_property_images_media_asset_id",
                table: "property_images",
                column: "media_asset_id");

            migrationBuilder.AddForeignKey(
                name: "FK_property_images_media_assets_media_asset_id",
                table: "property_images",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(
                """
                INSERT INTO media_assets (
                    id,
                    owner_user_id,
                    bucket_name,
                    object_key,
                    original_file_name,
                    stored_file_name,
                    content_type,
                    file_size,
                    file_hash,
                    scope,
                    visibility,
                    status,
                    linked_entity_type,
                    linked_entity_id,
                    created_at,
                    updated_at,
                    deleted_at
                )
                SELECT
                    (
                        substr(md5('property-image:' || pi.id::text), 1, 8) || '-' ||
                        substr(md5('property-image:' || pi.id::text), 9, 4) || '-' ||
                        substr(md5('property-image:' || pi.id::text), 13, 4) || '-' ||
                        substr(md5('property-image:' || pi.id::text), 17, 4) || '-' ||
                        substr(md5('property-image:' || pi.id::text), 21, 12)
                    )::uuid AS id,
                    COALESCE(rh.landlord_user_id, rh_from_room.landlord_user_id) AS owner_user_id,
                    'legacy-public-storage' AS bucket_name,
                    trim(both '/' from replace(pi.object_key, '\\', '/')) AS object_key,
                    split_part(trim(both '/' from replace(pi.object_key, '\\', '/')), '/', array_length(string_to_array(trim(both '/' from replace(pi.object_key, '\\', '/')), '/'), 1)) AS original_file_name,
                    split_part(trim(both '/' from replace(pi.object_key, '\\', '/')), '/', array_length(string_to_array(trim(both '/' from replace(pi.object_key, '\\', '/')), '/'), 1)) AS stored_file_name,
                    CASE lower(split_part(trim(both '/' from replace(pi.object_key, '\\', '/')), '.', array_length(string_to_array(trim(both '/' from replace(pi.object_key, '\\', '/')), '.'), 1)))
                        WHEN 'jpg' THEN 'image/jpeg'
                        WHEN 'jpeg' THEN 'image/jpeg'
                        WHEN 'png' THEN 'image/png'
                        WHEN 'webp' THEN 'image/webp'
                        WHEN 'pdf' THEN 'application/pdf'
                        ELSE 'application/octet-stream'
                    END AS content_type,
                    0 AS file_size,
                    NULL AS file_hash,
                    CASE
                        WHEN pi.rooming_house_id IS NOT NULL THEN 'RoomingHouseImage'
                        ELSE 'RoomImage'
                    END AS scope,
                    'Public' AS visibility,
                    'Linked' AS status,
                    'PropertyImage' AS linked_entity_type,
                    pi.id AS linked_entity_id,
                    pi.created_at AS created_at,
                    COALESCE(pi.created_at, now()) AS updated_at,
                    NULL AS deleted_at
                FROM property_images pi
                LEFT JOIN rooming_houses rh ON rh.id = pi.rooming_house_id
                LEFT JOIN rooms r ON r.id = pi.room_id
                LEFT JOIN rooming_houses rh_from_room ON rh_from_room.id = r.rooming_house_id
                LEFT JOIN media_assets existing_asset
                    ON existing_asset.object_key = trim(both '/' from replace(pi.object_key, '\\', '/'))
                WHERE pi.media_asset_id IS NULL
                    AND existing_asset.id IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE media_assets AS ma
                SET
                    owner_user_id = COALESCE(rh.landlord_user_id, rh_from_room.landlord_user_id),
                    scope = CASE
                        WHEN pi.rooming_house_id IS NOT NULL THEN 'RoomingHouseImage'
                        ELSE 'RoomImage'
                    END,
                    visibility = 'Public',
                    status = 'Linked',
                    linked_entity_type = 'PropertyImage',
                    linked_entity_id = pi.id,
                    deleted_at = NULL,
                    updated_at = now()
                FROM property_images pi
                LEFT JOIN rooming_houses rh ON rh.id = pi.rooming_house_id
                LEFT JOIN rooms r ON r.id = pi.room_id
                LEFT JOIN rooming_houses rh_from_room ON rh_from_room.id = r.rooming_house_id
                WHERE ma.object_key = trim(both '/' from replace(pi.object_key, '\\', '/'));
                """);

            migrationBuilder.Sql(
                """
                UPDATE property_images AS pi
                SET media_asset_id = ma.id
                FROM media_assets ma
                WHERE ma.object_key = trim(both '/' from replace(pi.object_key, '\\', '/'))
                    AND pi.media_asset_id IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_property_images_media_assets_media_asset_id",
                table: "property_images");

            migrationBuilder.DropIndex(
                name: "IX_property_images_media_asset_id",
                table: "property_images");

            migrationBuilder.Sql(
                """
                DELETE FROM media_assets
                WHERE linked_entity_type = 'PropertyImage';
                """);

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "property_images");
        }
    }
}
