using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715171000_CleanActiveTenantDemoHistory")]
    public partial class CleanActiveTenantDemoHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $demo$
                DECLARE
                    active_tenant_id uuid;
                    active_contract_id uuid;
                    active_request_id uuid;
                BEGIN
                    SELECT id
                    INTO active_tenant_id
                    FROM users
                    WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM'
                    LIMIT 1;

                    IF active_tenant_id IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT id, rental_request_id
                    INTO active_contract_id, active_request_id
                    FROM contracts
                    WHERE contract_number = 'DEMO-FLOW-ACTIVE-B201-20260601'
                      AND main_tenant_user_id = active_tenant_id
                    LIMIT 1;

                    IF active_contract_id IS NULL THEN
                        RETURN;
                    END IF;

                    CREATE TEMP TABLE demo_active_extra_contract_ids ON COMMIT DROP AS
                    SELECT id
                    FROM contracts
                    WHERE main_tenant_user_id = active_tenant_id
                      AND id <> active_contract_id;

                    CREATE TEMP TABLE demo_active_extra_request_ids ON COMMIT DROP AS
                    SELECT id
                    FROM rental_requests
                    WHERE tenant_user_id = active_tenant_id
                      AND id <> active_request_id
                    UNION
                    SELECT rental_request_id
                    FROM contracts
                    WHERE id IN (SELECT id FROM demo_active_extra_contract_ids)
                      AND rental_request_id IS NOT NULL
                      AND rental_request_id <> active_request_id;

                    CREATE TEMP TABLE demo_active_extra_deposit_ids ON COMMIT DROP AS
                    SELECT id
                    FROM room_deposits
                    WHERE tenant_user_id = active_tenant_id
                      AND id <> COALESCE((SELECT room_deposit_id FROM contracts WHERE id = active_contract_id), '00000000-0000-0000-0000-000000000000'::uuid)
                      AND (
                            rental_request_id IN (SELECT id FROM demo_active_extra_request_ids)
                         OR id IN (
                                SELECT room_deposit_id
                                FROM contracts
                                WHERE id IN (SELECT id FROM demo_active_extra_contract_ids)
                                  AND room_deposit_id IS NOT NULL
                            )
                      );

                    DELETE FROM review_reports
                    WHERE rooming_house_review_id IN (
                        SELECT id
                        FROM rooming_house_reviews
                        WHERE rental_contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                    );

                    DELETE FROM property_images
                    WHERE rooming_house_review_id IN (
                        SELECT id
                        FROM rooming_house_reviews
                        WHERE rental_contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                    );

                    DELETE FROM rooming_house_reviews
                    WHERE rental_contract_id IN (SELECT id FROM demo_active_extra_contract_ids);

                    DELETE FROM invoice_items
                    WHERE invoice_id IN (
                        SELECT id FROM invoices WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                    );

                    DELETE FROM meter_readings
                    WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids);

                    DELETE FROM invoices
                    WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids);

                    DELETE FROM contract_signatures
                    WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                       OR appendix_id IN (
                            SELECT id FROM contract_appendices WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                       );

                    DELETE FROM contract_files
                    WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                       OR appendix_id IN (
                            SELECT id FROM contract_appendices WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                       );

                    DELETE FROM contract_appendix_changes
                    WHERE appendix_id IN (
                        SELECT id FROM contract_appendices WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                    );

                    DELETE FROM contract_appendices
                    WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids);

                    DELETE FROM contract_occupant_documents
                    WHERE contract_occupant_id IN (
                        SELECT id FROM contract_occupants WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids)
                    );

                    DELETE FROM contract_occupants
                    WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids);

                    DELETE FROM contract_signing_envelopes
                    WHERE contract_id IN (SELECT id FROM demo_active_extra_contract_ids);

                    DELETE FROM contracts
                    WHERE id IN (SELECT id FROM demo_active_extra_contract_ids);

                    DELETE FROM room_deposits
                    WHERE id IN (SELECT id FROM demo_active_extra_deposit_ids);

                    DELETE FROM rental_requests
                    WHERE id IN (SELECT id FROM demo_active_extra_request_ids);

                    UPDATE users
                    SET display_name = 'Lê Quang Linh',
                        updated_at = now()
                    WHERE id = active_tenant_id;

                    UPDATE user_profiles
                    SET full_name = 'Lê Quang Linh',
                        updated_at = now()
                    WHERE user_id = active_tenant_id;

                    UPDATE kyc_verifications
                    SET ocr_full_name = 'Lê Quang Linh',
                        updated_at = now()
                    WHERE user_id = active_tenant_id;

                    UPDATE contract_occupants
                    SET full_name = 'Lê Quang Linh',
                        updated_at = now()
                    WHERE contract_id = active_contract_id
                      AND user_id = active_tenant_id;
                END $demo$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE users
                SET display_name = 'Học Tiếng Anh - Tenant Active',
                    updated_at = now()
                WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM';

                UPDATE user_profiles
                SET full_name = 'Học Tiếng Anh - Tenant Active',
                    updated_at = now()
                WHERE user_id = (
                    SELECT id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1
                );

                UPDATE kyc_verifications
                SET ocr_full_name = 'Học Tiếng Anh - Tenant Active',
                    updated_at = now()
                WHERE user_id = (
                    SELECT id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1
                );
                """);
        }
    }
}
