using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomingHouseLegalDocumentMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        substring(md5(rhld.rooming_house_id::text || ':legal-front') from 1 for 8) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-front') from 9 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-front') from 13 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-front') from 17 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-front') from 21 for 12)
                    )::uuid,
                    rh.landlord_user_id,
                    'legacy-private-storage',
                    rhld.front_image_object_key,
                    regexp_replace(rhld.front_image_object_key, '^.*/', ''),
                    regexp_replace(rhld.front_image_object_key, '^.*/', ''),
                    CASE
                        WHEN lower(rhld.front_image_object_key) LIKE '%%.jpg' OR lower(rhld.front_image_object_key) LIKE '%%.jpeg' THEN 'image/jpeg'
                        WHEN lower(rhld.front_image_object_key) LIKE '%%.png' THEN 'image/png'
                        WHEN lower(rhld.front_image_object_key) LIKE '%%.webp' THEN 'image/webp'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    NULL,
                    'RoomingHouseLegalDocument',
                    'Private',
                    'Linked',
                    'RoomingHouseLegalDocument',
                    rhld.rooming_house_id,
                    rhld.created_at,
                    rhld.updated_at,
                    NULL
                FROM rooming_house_legal_documents rhld
                INNER JOIN rooming_houses rh ON rh.id = rhld.rooming_house_id
                WHERE rhld.front_image_object_key IS NOT NULL
                  AND rhld.front_image_object_key <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = rhld.front_image_object_key
                  );
                """);

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
                        substring(md5(rhld.rooming_house_id::text || ':legal-back') from 1 for 8) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-back') from 9 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-back') from 13 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-back') from 17 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-back') from 21 for 12)
                    )::uuid,
                    rh.landlord_user_id,
                    'legacy-private-storage',
                    rhld.back_image_object_key,
                    regexp_replace(rhld.back_image_object_key, '^.*/', ''),
                    regexp_replace(rhld.back_image_object_key, '^.*/', ''),
                    CASE
                        WHEN lower(rhld.back_image_object_key) LIKE '%%.jpg' OR lower(rhld.back_image_object_key) LIKE '%%.jpeg' THEN 'image/jpeg'
                        WHEN lower(rhld.back_image_object_key) LIKE '%%.png' THEN 'image/png'
                        WHEN lower(rhld.back_image_object_key) LIKE '%%.webp' THEN 'image/webp'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    NULL,
                    'RoomingHouseLegalDocument',
                    'Private',
                    'Linked',
                    'RoomingHouseLegalDocument',
                    rhld.rooming_house_id,
                    rhld.created_at,
                    rhld.updated_at,
                    NULL
                FROM rooming_house_legal_documents rhld
                INNER JOIN rooming_houses rh ON rh.id = rhld.rooming_house_id
                WHERE rhld.back_image_object_key IS NOT NULL
                  AND rhld.back_image_object_key <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = rhld.back_image_object_key
                  );
                """);

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
                        substring(md5(rhld.rooming_house_id::text || ':legal-extra') from 1 for 8) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-extra') from 9 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-extra') from 13 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-extra') from 17 for 4) || '-' ||
                        substring(md5(rhld.rooming_house_id::text || ':legal-extra') from 21 for 12)
                    )::uuid,
                    rh.landlord_user_id,
                    'legacy-private-storage',
                    rhld.extra_image_object_key,
                    regexp_replace(rhld.extra_image_object_key, '^.*/', ''),
                    regexp_replace(rhld.extra_image_object_key, '^.*/', ''),
                    CASE
                        WHEN lower(rhld.extra_image_object_key) LIKE '%%.jpg' OR lower(rhld.extra_image_object_key) LIKE '%%.jpeg' THEN 'image/jpeg'
                        WHEN lower(rhld.extra_image_object_key) LIKE '%%.png' THEN 'image/png'
                        WHEN lower(rhld.extra_image_object_key) LIKE '%%.webp' THEN 'image/webp'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    NULL,
                    'RoomingHouseLegalDocument',
                    'Private',
                    'Linked',
                    'RoomingHouseLegalDocument',
                    rhld.rooming_house_id,
                    rhld.created_at,
                    rhld.updated_at,
                    NULL
                FROM rooming_house_legal_documents rhld
                INNER JOIN rooming_houses rh ON rh.id = rhld.rooming_house_id
                WHERE rhld.extra_image_object_key IS NOT NULL
                  AND rhld.extra_image_object_key <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = rhld.extra_image_object_key
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE media_assets ma
                SET owner_user_id = rh.landlord_user_id,
                    scope = 'RoomingHouseLegalDocument',
                    visibility = 'Private',
                    status = 'Linked',
                    linked_entity_type = 'RoomingHouseLegalDocument',
                    linked_entity_id = rhld.rooming_house_id,
                    deleted_at = NULL,
                    updated_at = GREATEST(ma.updated_at, rhld.updated_at)
                FROM rooming_house_legal_documents rhld
                INNER JOIN rooming_houses rh ON rh.id = rhld.rooming_house_id
                WHERE ma.object_key IN (
                    rhld.front_image_object_key,
                    rhld.back_image_object_key,
                    rhld.extra_image_object_key
                );
                """);

            migrationBuilder.Sql(
                """
                UPDATE rooming_house_legal_documents rhld
                SET front_media_asset_id = ma.id
                FROM media_assets ma
                WHERE rhld.front_media_asset_id IS NULL
                  AND ma.object_key = rhld.front_image_object_key
                  AND ma.linked_entity_type = 'RoomingHouseLegalDocument'
                  AND ma.linked_entity_id = rhld.rooming_house_id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE rooming_house_legal_documents rhld
                SET back_media_asset_id = ma.id
                FROM media_assets ma
                WHERE rhld.back_media_asset_id IS NULL
                  AND ma.object_key = rhld.back_image_object_key
                  AND ma.linked_entity_type = 'RoomingHouseLegalDocument'
                  AND ma.linked_entity_id = rhld.rooming_house_id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE rooming_house_legal_documents rhld
                SET extra_media_asset_id = ma.id
                FROM media_assets ma
                WHERE rhld.extra_media_asset_id IS NULL
                  AND rhld.extra_image_object_key IS NOT NULL
                  AND rhld.extra_image_object_key <> ''
                  AND ma.object_key = rhld.extra_image_object_key
                  AND ma.linked_entity_type = 'RoomingHouseLegalDocument'
                  AND ma.linked_entity_id = rhld.rooming_house_id;
                """);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_back_media_asset~",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_extra_media_asse~",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_rooming_house_legal_documents_media_assets_front_media_asse~",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_legal_documents_back_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_legal_documents_extra_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropIndex(
                name: "IX_rooming_house_legal_documents_front_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.Sql(
                """
                DELETE FROM media_assets
                WHERE linked_entity_type = 'RoomingHouseLegalDocument'
                  AND linked_entity_id IN (
                      SELECT rooming_house_id
                      FROM rooming_house_legal_documents
                  );
                """);

            migrationBuilder.DropColumn(
                name: "back_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "extra_media_asset_id",
                table: "rooming_house_legal_documents");

            migrationBuilder.DropColumn(
                name: "front_media_asset_id",
                table: "rooming_house_legal_documents");
        }
    }
}
