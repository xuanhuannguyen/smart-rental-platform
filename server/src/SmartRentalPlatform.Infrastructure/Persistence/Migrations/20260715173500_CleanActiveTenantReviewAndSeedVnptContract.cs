using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715173500_CleanActiveTenantReviewAndSeedVnptContract")]
    public partial class CleanActiveTenantReviewAndSeedVnptContract : Migration
    {
        private static bool LegacyDemoSeedIsDisabled() => true;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (LegacyDemoSeedIsDisabled())
            {
                // Legacy demo seed SQL targets pre-media columns. Current demo data is seeded by DevelopmentDataSeed.
                return;
            }

            migrationBuilder.Sql("""
                DO $demo$
                DECLARE
                    now_utc timestamptz := now();
                    active_tenant_id uuid;
                    primary_landlord_id uuid;
                    active_contract_id uuid;
                    active_house_id uuid;
                BEGIN
                    SELECT id INTO active_tenant_id
                    FROM users
                    WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM'
                    LIMIT 1;

                    SELECT id INTO primary_landlord_id
                    FROM users
                    WHERE normalized_email = 'NGUYENXUANHUAN21102005@GMAIL.COM'
                    LIMIT 1;

                    SELECT c.id, rh.id
                    INTO active_contract_id, active_house_id
                    FROM contracts c
                    JOIN rooms r ON r.id = c.room_id
                    JOIN rooming_houses rh ON rh.id = r.rooming_house_id
                    WHERE c.contract_number = 'DEMO-FLOW-ACTIVE-B201-20260601'
                    LIMIT 1;

                    IF active_tenant_id IS NULL OR active_contract_id IS NULL THEN
                        RAISE NOTICE 'CleanActiveTenantReviewAndSeedVnptContract skipped because active demo tenant/contract is missing.';
                        RETURN;
                    END IF;

                    DELETE FROM review_reports
                    WHERE rooming_house_review_id IN (
                        SELECT id
                        FROM rooming_house_reviews
                        WHERE tenant_user_id = active_tenant_id
                          AND (rental_contract_id = active_contract_id OR rooming_house_id = active_house_id)
                    );

                    DELETE FROM property_images
                    WHERE rooming_house_review_id IN (
                        SELECT id
                        FROM rooming_house_reviews
                        WHERE tenant_user_id = active_tenant_id
                          AND (rental_contract_id = active_contract_id OR rooming_house_id = active_house_id)
                    );

                    DELETE FROM rooming_house_reviews
                    WHERE tenant_user_id = active_tenant_id
                      AND (rental_contract_id = active_contract_id OR rooming_house_id = active_house_id);

                    UPDATE contract_files
                    SET storage_object_key = 'demo-flow/contracts/active-b201-signed-vnpt.pdf',
                        "FileUrl" = '/uploads/demo-flow/contracts/active-b201-signed-vnpt.pdf',
                        sha256_hash = 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce',
                        is_legally_signed = TRUE,
                        created_at = TIMESTAMPTZ '2026-04-25 15:00:00Z'
                    WHERE contract_id = active_contract_id
                      AND appendix_id IS NULL
                      AND purpose = 'SignedLegalDocument';

                    UPDATE contract_files
                    SET storage_object_key = 'demo-flow/contracts/active-b201-preview-vnpt.pdf',
                        "FileUrl" = '/uploads/demo-flow/contracts/active-b201-preview-vnpt.pdf',
                        sha256_hash = '58bb3ebd5d963a985b2c5e0522bdc2d4d4ed450552b976c03ebe34821382b8ab',
                        is_legally_signed = FALSE,
                        created_at = TIMESTAMPTZ '2026-04-25 14:00:00Z'
                    WHERE contract_id = active_contract_id
                      AND appendix_id IS NULL
                      AND purpose = 'Preview';

                    UPDATE contract_signatures
                    SET signature_method = 'VnptSmsOtp',
                        status = 'Signed',
                        provider = 'Vnpt',
                        provider_envelope_id = 'VNPT-ECONTRACT-B201-20260425',
                        provider_participant_id = 'VNPT-PART-LANDLORD-0901000002',
                        certificate_serial_number = 'VNPT-CA-2026-000102',
                        certificate_subject = 'CN=Nguyễn Xuân Huân, O=VNPT SmartCA, C=VN',
                        certificate_issuer = 'VNPT Certification Authority',
                        signed_file_sha256_hash = 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce',
                        provider_evidence_json = '{"provider":"VNPT eContract","auth_method":"SMS_OTP","otp_delivery":"sms","otp_verified_at":"2026-04-25T14:00:00Z","masked_phone":"090****002","transaction_id":"VNPT-OTP-B201-LANDLORD-20260425","document_number":"HD-202604250900-B201-XUANHUAN"}',
                        notified_at = TIMESTAMPTZ '2026-04-25 13:45:00Z',
                        signed_at = TIMESTAMPTZ '2026-04-25 14:00:00Z',
                        user_agent = 'VNPT eContract Demo Seed',
                        created_at = TIMESTAMPTZ '2026-04-25 13:45:00Z'
                    WHERE contract_id = active_contract_id
                      AND signer_user_id = primary_landlord_id
                      AND signer_role = 'Landlord';

                    UPDATE contract_signatures
                    SET signature_method = 'VnptSmsOtp',
                        status = 'Signed',
                        provider = 'Vnpt',
                        provider_envelope_id = 'VNPT-ECONTRACT-B201-20260425',
                        provider_participant_id = 'VNPT-PART-TENANT-0901000004',
                        certificate_serial_number = 'VNPT-CA-2026-000104',
                        certificate_subject = 'CN=Lê Quang Linh, O=VNPT SmartCA, C=VN',
                        certificate_issuer = 'VNPT Certification Authority',
                        signed_file_sha256_hash = 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce',
                        provider_evidence_json = '{"provider":"VNPT eContract","auth_method":"SMS_OTP","otp_delivery":"sms","otp_verified_at":"2026-04-25T15:00:00Z","masked_phone":"090****004","transaction_id":"VNPT-OTP-B201-TENANT-20260425","document_number":"HD-202604250900-B201-XUANHUAN"}',
                        notified_at = TIMESTAMPTZ '2026-04-25 14:45:00Z',
                        signed_at = TIMESTAMPTZ '2026-04-25 15:00:00Z',
                        user_agent = 'VNPT eContract Demo Seed',
                        created_at = TIMESTAMPTZ '2026-04-25 14:45:00Z'
                    WHERE contract_id = active_contract_id
                      AND signer_user_id = active_tenant_id
                      AND signer_role = 'Tenant';

                    UPDATE rooming_houses h
                    SET average_rating = COALESCE(r.avg_rating, 0),
                        total_reviews = COALESCE(r.total_reviews, 0),
                        updated_at = now_utc
                    FROM (
                        SELECT rooming_house_id, COUNT(*)::int AS total_reviews, AVG(rating)::numeric(3,2) AS avg_rating
                        FROM rooming_house_reviews
                        WHERE is_hidden = FALSE
                          AND moderation_status = 'Approved'
                        GROUP BY rooming_house_id
                    ) r
                    WHERE h.id = r.rooming_house_id
                      AND h.id = active_house_id;

                    UPDATE rooming_houses h
                    SET average_rating = 0,
                        total_reviews = 0,
                        updated_at = now_utc
                    WHERE h.id = active_house_id
                      AND NOT EXISTS (
                        SELECT 1
                        FROM rooming_house_reviews rr
                        WHERE rr.rooming_house_id = h.id
                          AND rr.is_hidden = FALSE
                          AND rr.moderation_status = 'Approved'
                      );
                END $demo$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (LegacyDemoSeedIsDisabled())
            {
                // No-op: matching legacy demo seed Up() is disabled after media schema cutover.
                return;
            }

            migrationBuilder.Sql("""
                UPDATE contract_files
                SET storage_object_key = 'demo-flow/contracts/active-b201-signed.pdf',
                    "FileUrl" = '/uploads/demo-flow/contracts/active-b201-signed.pdf'
                WHERE storage_object_key = 'demo-flow/contracts/active-b201-signed-vnpt.pdf';

                UPDATE contract_files
                SET storage_object_key = 'demo-flow/contracts/active-b201-preview.pdf',
                    "FileUrl" = '/uploads/demo-flow/contracts/active-b201-preview.pdf'
                WHERE storage_object_key = 'demo-flow/contracts/active-b201-preview-vnpt.pdf';
                """);
        }
    }
}
