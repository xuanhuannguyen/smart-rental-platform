using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKycVerificationMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                        substring(md5(kv.id::text || ':front') from 1 for 8) || '-' ||
                        substring(md5(kv.id::text || ':front') from 9 for 4) || '-' ||
                        substring(md5(kv.id::text || ':front') from 13 for 4) || '-' ||
                        substring(md5(kv.id::text || ':front') from 17 for 4) || '-' ||
                        substring(md5(kv.id::text || ':front') from 21 for 12)
                    )::uuid,
                    kv.user_id,
                    'legacy-private-storage',
                    kv.front_image_object_key,
                    regexp_replace(kv.front_image_object_key, '^.*/', ''),
                    regexp_replace(kv.front_image_object_key, '^.*/', ''),
                    CASE
                        WHEN lower(kv.front_image_object_key) LIKE '%%.jpg' OR lower(kv.front_image_object_key) LIKE '%%.jpeg' THEN 'image/jpeg'
                        WHEN lower(kv.front_image_object_key) LIKE '%%.png' THEN 'image/png'
                        WHEN lower(kv.front_image_object_key) LIKE '%%.webp' THEN 'image/webp'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    NULL,
                    'KycDocument',
                    'Private',
                    'Linked',
                    'KycVerification',
                    kv.id,
                    kv.created_at,
                    kv.updated_at,
                    NULL
                FROM kyc_verifications kv
                WHERE kv.front_image_object_key IS NOT NULL
                  AND kv.front_image_object_key <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = kv.front_image_object_key
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
                        substring(md5(kv.id::text || ':back') from 1 for 8) || '-' ||
                        substring(md5(kv.id::text || ':back') from 9 for 4) || '-' ||
                        substring(md5(kv.id::text || ':back') from 13 for 4) || '-' ||
                        substring(md5(kv.id::text || ':back') from 17 for 4) || '-' ||
                        substring(md5(kv.id::text || ':back') from 21 for 12)
                    )::uuid,
                    kv.user_id,
                    'legacy-private-storage',
                    kv.back_image_object_key,
                    regexp_replace(kv.back_image_object_key, '^.*/', ''),
                    regexp_replace(kv.back_image_object_key, '^.*/', ''),
                    CASE
                        WHEN lower(kv.back_image_object_key) LIKE '%%.jpg' OR lower(kv.back_image_object_key) LIKE '%%.jpeg' THEN 'image/jpeg'
                        WHEN lower(kv.back_image_object_key) LIKE '%%.png' THEN 'image/png'
                        WHEN lower(kv.back_image_object_key) LIKE '%%.webp' THEN 'image/webp'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    NULL,
                    'KycDocument',
                    'Private',
                    'Linked',
                    'KycVerification',
                    kv.id,
                    kv.created_at,
                    kv.updated_at,
                    NULL
                FROM kyc_verifications kv
                WHERE kv.back_image_object_key IS NOT NULL
                  AND kv.back_image_object_key <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = kv.back_image_object_key
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
                        substring(md5(kv.id::text || ':selfie') from 1 for 8) || '-' ||
                        substring(md5(kv.id::text || ':selfie') from 9 for 4) || '-' ||
                        substring(md5(kv.id::text || ':selfie') from 13 for 4) || '-' ||
                        substring(md5(kv.id::text || ':selfie') from 17 for 4) || '-' ||
                        substring(md5(kv.id::text || ':selfie') from 21 for 12)
                    )::uuid,
                    kv.user_id,
                    'legacy-private-storage',
                    kv.selfie_image_object_key,
                    regexp_replace(kv.selfie_image_object_key, '^.*/', ''),
                    regexp_replace(kv.selfie_image_object_key, '^.*/', ''),
                    CASE
                        WHEN lower(kv.selfie_image_object_key) LIKE '%%.jpg' OR lower(kv.selfie_image_object_key) LIKE '%%.jpeg' THEN 'image/jpeg'
                        WHEN lower(kv.selfie_image_object_key) LIKE '%%.png' THEN 'image/png'
                        WHEN lower(kv.selfie_image_object_key) LIKE '%%.webp' THEN 'image/webp'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    NULL,
                    'KycDocument',
                    'Private',
                    'Linked',
                    'KycVerification',
                    kv.id,
                    kv.created_at,
                    kv.updated_at,
                    NULL
                FROM kyc_verifications kv
                WHERE kv.selfie_image_object_key IS NOT NULL
                  AND kv.selfie_image_object_key <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = kv.selfie_image_object_key
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE kyc_verifications kv
                SET front_media_asset_id = ma.id
                FROM media_assets ma
                WHERE kv.front_media_asset_id IS NULL
                  AND ma.object_key = kv.front_image_object_key
                  AND ma.linked_entity_type = 'KycVerification'
                  AND ma.linked_entity_id = kv.id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE kyc_verifications kv
                SET back_media_asset_id = ma.id
                FROM media_assets ma
                WHERE kv.back_media_asset_id IS NULL
                  AND ma.object_key = kv.back_image_object_key
                  AND ma.linked_entity_type = 'KycVerification'
                  AND ma.linked_entity_id = kv.id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE kyc_verifications kv
                SET selfie_media_asset_id = ma.id
                FROM media_assets ma
                WHERE kv.selfie_media_asset_id IS NULL
                  AND ma.object_key = kv.selfie_image_object_key
                  AND ma.linked_entity_type = 'KycVerification'
                  AND ma.linked_entity_id = kv.id;
                """);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_kyc_verifications_media_assets_back_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropForeignKey(
                name: "FK_kyc_verifications_media_assets_front_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropForeignKey(
                name: "FK_kyc_verifications_media_assets_selfie_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_back_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_front_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropIndex(
                name: "IX_kyc_verifications_selfie_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.Sql(
                """
                DELETE FROM media_assets
                WHERE linked_entity_type = 'KycVerification'
                  AND linked_entity_id IN (
                      SELECT id
                      FROM kyc_verifications
                  );
                """);

            migrationBuilder.DropColumn(
                name: "back_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "front_media_asset_id",
                table: "kyc_verifications");

            migrationBuilder.DropColumn(
                name: "selfie_media_asset_id",
                table: "kyc_verifications");
        }
    }
}
