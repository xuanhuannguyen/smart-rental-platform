using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartRentalPlatform.Infrastructure.Persistence;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260713153000_CleanupLegacySampleMediaReferences")]
    public partial class CleanupLegacySampleMediaReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                delete from property_images
                where object_key like 'demo/%'
                   or object_key like 'kfc-scenario/%'
                   or object_key like 'seed/%'
                   or image_url like '/uploads/demo/%'
                   or image_url like '/uploads/kfc-scenario/%'
                   or image_url like '/uploads/seed/%';
                """);

            migrationBuilder.Sql("""
                delete from rooming_house_legal_documents
                where front_image_object_key like 'demo/%'
                   or front_image_object_key like 'kfc-scenario/%'
                   or front_image_object_key like 'seed/%'
                   or back_image_object_key like 'demo/%'
                   or back_image_object_key like 'kfc-scenario/%'
                   or back_image_object_key like 'seed/%'
                   or extra_image_object_key like 'demo/%'
                   or extra_image_object_key like 'kfc-scenario/%'
                   or extra_image_object_key like 'seed/%';
                """);

            migrationBuilder.Sql("""
                update kyc_verifications
                set front_image_object_key = '',
                    back_image_object_key = '',
                    selfie_image_object_key = '',
                    front_media_asset_id = null,
                    back_media_asset_id = null,
                    selfie_media_asset_id = null
                where front_image_object_key like 'demo/%'
                   or front_image_object_key like 'kfc-scenario/%'
                   or back_image_object_key like 'demo/%'
                   or back_image_object_key like 'kfc-scenario/%'
                   or selfie_image_object_key like 'demo/%'
                   or selfie_image_object_key like 'kfc-scenario/%';
                """);

            migrationBuilder.Sql("""
                delete from contract_occupant_documents
                where front_image_object_key like 'demo/%'
                   or front_image_object_key like 'kfc-scenario/%'
                   or back_image_object_key like 'demo/%'
                   or back_image_object_key like 'kfc-scenario/%'
                   or extra_image_object_key like 'demo/%'
                   or extra_image_object_key like 'kfc-scenario/%';
                """);

            migrationBuilder.Sql("""
                update rooming_house_rules
                set pdf_object_key = '',
                    media_asset_id = null
                where pdf_object_key like 'demo/%'
                   or pdf_object_key like 'kfc-scenario/%'
                   or pdf_object_key like 'seed/%';
                """);

            migrationBuilder.Sql("""
                delete from contract_files
                where storage_object_key like 'demo/%'
                   or storage_object_key like 'kfc-scenario/%'
                   or storage_object_key like 'seed/%';
                """);

            migrationBuilder.Sql("""
                update meter_readings
                set proof_image_object_key = null,
                    proof_media_asset_id = null
                where proof_image_object_key like 'demo/%'
                   or proof_image_object_key like 'kfc-scenario/%'
                   or proof_image_object_key like 'seed/%';
                """);

            migrationBuilder.Sql("""
                update users
                set avatar_url = null,
                    avatar_media_asset_id = null
                where avatar_url like '/uploads/%'
                   or avatar_url like 'uploads/%'
                   or avatar_url like '/api/media/public/%'
                   or avatar_url like 'api/media/public/%'
                   or avatar_url like 'public/%'
                   or avatar_url like 'demo/%'
                   or avatar_url like 'kfc-scenario/%'
                   or avatar_url like 'seed/%';
                """);

            migrationBuilder.Sql("""
                delete from media_audit_logs
                where media_asset_id in (
                    select id
                    from media_assets
                    where object_key like 'demo/%'
                       or object_key like 'kfc-scenario/%'
                       or object_key like 'seed/%'
                       or bucket_name = 'legacy-media'
                );
                """);

            migrationBuilder.Sql("""
                delete from media_assets
                where object_key like 'demo/%'
                   or object_key like 'kfc-scenario/%'
                   or object_key like 'seed/%'
                   or bucket_name = 'legacy-media';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible data hygiene migration. Historical sample media rows pointed to
            // non-existent local files/bucket objects and should not be recreated.
        }
    }
}
