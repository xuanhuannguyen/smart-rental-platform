using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContractFileMediaAssetLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                table: "contract_files",
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
                    cf.id,
                    NULL,
                    'local-media',
                    cf.storage_object_key,
                    regexp_replace(cf.storage_object_key, '^.*/', ''),
                    regexp_replace(cf.storage_object_key, '^.*/', ''),
                    CASE
                        WHEN lower(cf.storage_object_key) LIKE '%%.pdf' THEN 'application/pdf'
                        ELSE 'application/octet-stream'
                    END,
                    0,
                    NULL,
                    'ContractPdf',
                    'Private',
                    'Linked',
                    'ContractFile',
                    cf.id,
                    cf.created_at,
                    cf.created_at,
                    NULL
                FROM contract_files cf
                WHERE cf.appendix_id IS NULL
                  AND cf.media_asset_id IS NULL
                  AND cf.storage_object_key IS NOT NULL
                  AND cf.storage_object_key <> ''
                  AND NOT EXISTS (
                      SELECT 1
                      FROM media_assets ma
                      WHERE ma.object_key = cf.storage_object_key
                  );
                """);

            migrationBuilder.Sql(
                """
                UPDATE contract_files cf
                SET media_asset_id = ma.id
                FROM media_assets ma
                WHERE cf.appendix_id IS NULL
                  AND cf.media_asset_id IS NULL
                  AND ma.linked_entity_type = 'ContractFile'
                  AND ma.linked_entity_id = cf.id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_contract_files_media_asset_id",
                table: "contract_files",
                column: "media_asset_id");

            migrationBuilder.AddForeignKey(
                name: "FK_contract_files_media_assets_media_asset_id",
                table: "contract_files",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contract_files_media_assets_media_asset_id",
                table: "contract_files");

            migrationBuilder.DropIndex(
                name: "IX_contract_files_media_asset_id",
                table: "contract_files");

            migrationBuilder.Sql(
                """
                DELETE FROM media_assets
                WHERE linked_entity_type = 'ContractFile'
                  AND linked_entity_id IN (
                      SELECT id
                      FROM contract_files
                      WHERE appendix_id IS NULL
                  );
                """);

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                table: "contract_files");
        }
    }
}
