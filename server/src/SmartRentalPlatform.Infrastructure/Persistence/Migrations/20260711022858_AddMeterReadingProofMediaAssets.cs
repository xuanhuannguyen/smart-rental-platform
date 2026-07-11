using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMeterReadingProofMediaAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "proof_media_asset_id",
                table: "meter_readings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_proof_media_asset_id",
                table: "meter_readings",
                column: "proof_media_asset_id");

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
                    scope,
                    visibility,
                    status,
                    linked_entity_type,
                    linked_entity_id,
                    created_at,
                    updated_at
                )
                SELECT
                    gen_random_uuid(),
                    m.recorded_by_landlord_user_id,
                    'legacy-private-storage',
                    m.proof_image_object_key,
                    split_part(m.proof_image_object_key, '/', array_length(string_to_array(m.proof_image_object_key, '/'), 1)),
                    split_part(m.proof_image_object_key, '/', array_length(string_to_array(m.proof_image_object_key, '/'), 1)),
                    CASE
                        WHEN lower(m.proof_image_object_key) LIKE '%.jpg' OR lower(m.proof_image_object_key) LIKE '%.jpeg' THEN 'image/jpeg'
                        WHEN lower(m.proof_image_object_key) LIKE '%.png' THEN 'image/png'
                        WHEN lower(m.proof_image_object_key) LIKE '%.webp' THEN 'image/webp'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    'MeterReadingImage',
                    'Private',
                    'Linked',
                    'MeterReading',
                    m.id,
                    m.created_at,
                    m.updated_at
                FROM meter_readings m
                WHERE m.proof_image_object_key IS NOT NULL
                  AND btrim(m.proof_image_object_key) <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = m.proof_image_object_key
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE meter_readings m
                SET proof_media_asset_id = ma.id
                FROM media_assets ma
                WHERE m.proof_image_object_key IS NOT NULL
                  AND btrim(m.proof_image_object_key) <> ''
                  AND ma.object_key = m.proof_image_object_key;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_meter_readings_media_assets_proof_media_asset_id",
                table: "meter_readings",
                column: "proof_media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_meter_readings_media_assets_proof_media_asset_id",
                table: "meter_readings");

            migrationBuilder.DropIndex(
                name: "IX_meter_readings_proof_media_asset_id",
                table: "meter_readings");

            migrationBuilder.DropColumn(
                name: "proof_media_asset_id",
                table: "meter_readings");
        }
    }
}
