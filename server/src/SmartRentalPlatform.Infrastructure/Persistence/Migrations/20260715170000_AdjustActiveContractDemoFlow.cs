using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715170000_AdjustActiveContractDemoFlow")]
    public partial class AdjustActiveContractDemoFlow : Migration
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
                CREATE OR REPLACE FUNCTION pg_temp.demo_flow_uuid(input text) RETURNS uuid AS $fn$
                    SELECT (
                        substr(md5(input), 1, 8) || '-' ||
                        substr(md5(input), 9, 4) || '-' ||
                        substr(md5(input), 13, 4) || '-' ||
                        substr(md5(input), 17, 4) || '-' ||
                        substr(md5(input), 21, 12)
                    )::uuid;
                $fn$ LANGUAGE SQL IMMUTABLE;

                DO $demo$
                DECLARE
                    seeded_at timestamptz := TIMESTAMPTZ '2026-07-15 00:00:00Z';
                    now_utc timestamptz := now();
                    admin_user_id uuid;
                    active_tenant_id uuid;
                    primary_landlord_id uuid;
                    new_occupant_user_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-NEW-OCCUPANT');
                    active_contract_id uuid;
                    active_wallet_id uuid;
                    landlord_wallet_id uuid;
                    july_correct_invoice_id uuid;
                    blocking_invoice_id uuid;
                    final_invoice_id uuid;
                    active_deposit_id uuid;
                    reading_electric_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-ELECTRIC-202607');
                    reading_water_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-WATER-202607');
                    password_hash text;
                BEGIN
                    SELECT id INTO admin_user_id FROM users WHERE normalized_email = 'ADMIN.DEMO@EXAMPLE.COM' LIMIT 1;
                    SELECT id INTO active_tenant_id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1;
                    SELECT id INTO primary_landlord_id FROM users WHERE normalized_email = 'NGUYENXUANHUAN21102005@GMAIL.COM' LIMIT 1;
                    SELECT id INTO active_contract_id FROM contracts WHERE contract_number = 'DEMO-FLOW-ACTIVE-B201-20260601' LIMIT 1;
                    SELECT id INTO active_wallet_id FROM wallet_accounts WHERE user_id = active_tenant_id LIMIT 1;
                    SELECT id INTO landlord_wallet_id FROM wallet_accounts WHERE user_id = primary_landlord_id LIMIT 1;
                    SELECT id INTO july_correct_invoice_id FROM invoices WHERE invoice_no = 'DEMO-FLOW-INV-202607-CORRECT' LIMIT 1;
                    SELECT id INTO blocking_invoice_id FROM invoices WHERE invoice_no = 'DEMO-FLOW-INV-BLOCK-TERMINATE' LIMIT 1;
                    SELECT id INTO final_invoice_id FROM invoices WHERE invoice_no = 'DEMO-FLOW-INV-FINAL-DRAFT' LIMIT 1;
                    SELECT id INTO active_deposit_id FROM room_deposits WHERE note LIKE 'DEMO-FLOW:%active tenant%' LIMIT 1;
                    SELECT u.password_hash INTO password_hash FROM users u WHERE u.normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1;

                    IF active_tenant_id IS NULL OR active_contract_id IS NULL THEN
                        RAISE NOTICE 'AdjustActiveContractDemoFlow skipped because demo active tenant/contract is not seeded.';
                        RETURN;
                    END IF;

                    INSERT INTO users (id, email, normalized_email, phone_number, password_hash, display_name, avatar_url, status, onboarding_status, email_confirmed, phone_confirmed, access_failed_count, lockout_end_at, last_login_at, created_at, updated_at, deleted_at)
                    VALUES (new_occupant_user_id, 'demo.flow.newoccupant@example.com', 'DEMO.FLOW.NEWOCCUPANT@EXAMPLE.COM', '0901000006', password_hash, 'Người Ở Mới Demo', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL)
                    ON CONFLICT (normalized_email) DO UPDATE SET
                        email = EXCLUDED.email,
                        phone_number = EXCLUDED.phone_number,
                        password_hash = COALESCE(EXCLUDED.password_hash, users.password_hash),
                        display_name = EXCLUDED.display_name,
                        status = 'Active',
                        onboarding_status = 'Completed',
                        email_confirmed = TRUE,
                        updated_at = now_utc,
                        deleted_at = NULL;

                    SELECT id INTO new_occupant_user_id FROM users WHERE normalized_email = 'DEMO.FLOW.NEWOCCUPANT@EXAMPLE.COM' LIMIT 1;

                    INSERT INTO user_profiles (user_id, full_name, date_of_birth, gender, address_line, verified_citizen_id_masked, emergency_contact_name, emergency_contact_phone, created_at, updated_at)
                    VALUES (new_occupant_user_id, 'Người Ở Mới Demo', DATE '2001-06-15', 'Female', 'Đà Nẵng', '079********106', 'Demo Support', '0999000006', seeded_at, now_utc)
                    ON CONFLICT (user_id) DO UPDATE SET
                        full_name = EXCLUDED.full_name,
                        date_of_birth = EXCLUDED.date_of_birth,
                        gender = EXCLUDED.gender,
                        address_line = EXCLUDED.address_line,
                        verified_citizen_id_masked = EXCLUDED.verified_citizen_id_masked,
                        updated_at = now_utc;

                    INSERT INTO user_roles (user_id, role_id, created_at)
                    VALUES (new_occupant_user_id, 2, seeded_at)
                    ON CONFLICT DO NOTHING;

                    INSERT INTO kyc_verifications (id, user_id, document_type, ekyc_provider, ekyc_session_id, front_image_object_key, back_image_object_key, selfie_image_object_key, selfie_capture_method, ocr_full_name, ocr_citizen_id_masked, citizen_id_hash, document_number_encrypted, ocr_date_of_birth, ocr_gender, ocr_address, ocr_confidence, document_check_result, face_match_score, face_match_result, liveness_result, ekyc_result, ekyc_error_code, ekyc_error_message, risk_level, status, reviewed_by_admin_id, rejected_reason, submitted_at, reviewed_at, created_at, updated_at)
                    VALUES (pg_temp.demo_flow_uuid('DEMO-FLOW-KYC-NEW-OCCUPANT'), new_occupant_user_id, 'CCCD', 'Vnpt', 'demo-flow-new-occupant', 'demo-flow/kyc/new-occupant/front.jpg', 'demo-flow/kyc/new-occupant/back.jpg', 'demo-flow/kyc/new-occupant/selfie.jpg', 'Upload', 'Người Ở Mới Demo', '079********106', encode(sha256('demo-flow-new-occupant'::bytea), 'hex'), 'encrypted-demo-flow-106', DATE '2001-06-15', 'Female', 'Đà Nẵng', 0.9900, 'Valid', 0.9800, 'Matched', 'Passed', 'Passed', NULL, NULL, 'Low', 'Approved', admin_user_id, NULL, seeded_at, seeded_at, seeded_at, now_utc)
                    ON CONFLICT (id) DO UPDATE SET
                        status = 'Approved',
                        ekyc_result = 'Passed',
                        reviewed_by_admin_id = EXCLUDED.reviewed_by_admin_id,
                        reviewed_at = EXCLUDED.reviewed_at,
                        updated_at = now_utc;

                    DELETE FROM contract_occupant_documents
                    WHERE contract_occupant_id IN (
                        SELECT id
                        FROM contract_occupants
                        WHERE contract_id = active_contract_id
                          AND id <> pg_temp.demo_flow_uuid('DEMO-FLOW-OCC-ACTIVE-MAIN')
                    );

                    DELETE FROM contract_occupants
                    WHERE contract_id = active_contract_id
                      AND id <> pg_temp.demo_flow_uuid('DEMO-FLOW-OCC-ACTIVE-MAIN');

                    UPDATE rooms
                    SET max_occupants = 2,
                        updated_at = now_utc
                    WHERE id = (SELECT room_id FROM contracts WHERE id = active_contract_id);

                    UPDATE contracts
                    SET room_snapshot = jsonb_set(COALESCE(room_snapshot, '{}'::jsonb), '{MaxOccupants}', '2'::jsonb, TRUE),
                        status = 'Active',
                        termination_date = NULL,
                        termination_type = NULL,
                        updated_at = now_utc
                    WHERE id = active_contract_id;

                    UPDATE wallet_accounts
                    SET balance = 3670000,
                        reserved_balance = 0,
                        status = 'Active',
                        updated_at = now_utc
                    WHERE id = active_wallet_id;

                    UPDATE wallet_accounts
                    SET reserved_balance = 3950000,
                        updated_at = now_utc
                    WHERE id = landlord_wallet_id;

                    UPDATE payment_transactions
                    SET amount = 3670000,
                        gateway_response_message = 'Seed ví active tenant còn 3.670.000, thiếu đúng 10.000 so với hóa đơn 3.680.000.',
                        updated_at = now_utc
                    WHERE idempotency_key = 'demo-flow:active-tenant-seed-topup';

                    UPDATE wallet_transactions
                    SET amount = 3670000,
                        balance_after = 3670000,
                        description = 'DEMO-FLOW: hoctienganh4english@gmail.com bắt đầu với 3.670.000, thiếu 10.000 so với hóa đơn demo.'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-ACTIVE-SEED');

                    UPDATE wallet_transactions
                    SET balance_before = 8300000,
                        balance_after = 3670000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-JUNE-TENANT');

                    UPDATE meter_readings
                    SET proof_image_object_key = 'rooming-houses/20260713011217-61c006cd95d24c739170d8cca70f1467.png',
                        ai_reading = 1341,
                        ai_raw_text = 'AI OCR: điện hiện tại 1341 kWh',
                        updated_at = now_utc
                    WHERE id = reading_electric_id;

                    UPDATE meter_readings
                    SET proof_image_object_key = 'rooming-houses/20260713011217-c5b92e2cdaec4e729499d56c64dd5557.png',
                        ai_reading = 96,
                        ai_raw_text = 'AI OCR: nước hiện tại 96 m3',
                        updated_at = now_utc
                    WHERE id = reading_water_id;

                    UPDATE invoices
                    SET status = 'Issued',
                        rent_amount = 3352000,
                        utility_amount = 508000,
                        service_amount = 120000,
                        discount_amount = 300000,
                        total_amount = 3680000,
                        paid_at = NULL,
                        cancelled_at = NULL,
                        cancel_reason = NULL,
                        wallet_transfer_group_id = NULL,
                        note = 'Hóa đơn đang chờ thanh toán, có ảnh điện/nước từ AI OCR; ví tenant thiếu đúng 10.000 nên phải nạp thêm trước khi trả.',
                        updated_at = now_utc
                    WHERE id = july_correct_invoice_id;

                    DELETE FROM invoice_items WHERE invoice_id = blocking_invoice_id;
                    DELETE FROM invoices WHERE id = blocking_invoice_id;

                    UPDATE invoices
                    SET status = 'Draft',
                        total_amount = 80000,
                        note = 'Hóa đơn kỳ cuối để landlord phát hành sau khi tenant chấm dứt trước hạn; cọc bị chuyển về ví chủ trọ.',
                        updated_at = now_utc
                    WHERE id = final_invoice_id;

                    UPDATE room_deposits
                    SET status = 'Paid',
                        refunded_at = NULL,
                        forfeited_at = NULL,
                        refund_amount = NULL,
                        forfeited_amount = NULL,
                        note = 'DEMO-FLOW: active tenant đã thanh toán cọc; nếu chấm dứt trước hạn thì cọc chuyển về ví chủ trọ.',
                        updated_at = now_utc
                    WHERE id = active_deposit_id;
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
                CREATE OR REPLACE FUNCTION pg_temp.demo_flow_uuid(input text) RETURNS uuid AS $fn$
                    SELECT (
                        substr(md5(input), 1, 8) || '-' ||
                        substr(md5(input), 9, 4) || '-' ||
                        substr(md5(input), 13, 4) || '-' ||
                        substr(md5(input), 17, 4) || '-' ||
                        substr(md5(input), 21, 12)
                    )::uuid;
                $fn$ LANGUAGE SQL IMMUTABLE;

                DO $demo$
                DECLARE
                    active_contract_id uuid;
                    active_wallet_id uuid;
                    active_tenant_id uuid;
                    room_b201_id uuid;
                    primary_landlord_id uuid;
                    blocking_invoice_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-BLOCK-TERMINATE');
                    trash_service_id uuid;
                BEGIN
                    SELECT id INTO active_tenant_id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1;
                    SELECT id INTO primary_landlord_id FROM users WHERE normalized_email = 'NGUYENXUANHUAN21102005@GMAIL.COM' LIMIT 1;
                    SELECT id INTO active_contract_id FROM contracts WHERE contract_number = 'DEMO-FLOW-ACTIVE-B201-20260601' LIMIT 1;
                    SELECT id INTO active_wallet_id FROM wallet_accounts WHERE user_id = active_tenant_id LIMIT 1;
                    SELECT room_id INTO room_b201_id FROM contracts WHERE id = active_contract_id LIMIT 1;
                    SELECT id INTO trash_service_id FROM billing_service_types WHERE lower(name) = lower('Rác') LIMIT 1;

                    UPDATE wallet_accounts SET balance = 3700000, updated_at = now() WHERE id = active_wallet_id;

                    INSERT INTO contract_occupants (id, contract_id, user_id, guardian_occupant_id, full_name, phone_number, date_of_birth, relationship_to_main_tenant, move_in_date, move_out_date, status, created_at, updated_at)
                    VALUES (pg_temp.demo_flow_uuid('DEMO-FLOW-OCC-ACTIVE-FRIEND'), active_contract_id, NULL, NULL, 'Bạn cùng phòng Demo', '0901999000', DATE '1999-09-09', 'Bạn cùng phòng', DATE '2026-06-01', NULL, 'Active', TIMESTAMPTZ '2026-05-25 15:00:00Z', now())
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO invoices (id, contract_id, room_id, tenant_user_id, landlord_user_id, invoice_no, billing_period_start, billing_period_end, issue_date, due_date, rent_amount, utility_amount, service_amount, discount_amount, total_amount, status, note, sent_at, paid_at, cancelled_at, cancel_reason, wallet_transfer_group_id, created_at, updated_at)
                    VALUES (blocking_invoice_id, active_contract_id, room_b201_id, active_tenant_id, primary_landlord_id, 'DEMO-FLOW-INV-BLOCK-TERMINATE', DATE '2026-05-01', DATE '2026-05-31', DATE '2026-07-12', DATE '2026-07-13', 0, 0, 70000, 0, 70000, 'Overdue', 'Hóa đơn nhỏ cố ý để block chấm dứt hợp đồng.', TIMESTAMPTZ '2026-07-12 09:00:00Z', NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-12 09:00:00Z', now())
                    ON CONFLICT (invoice_no) DO NOTHING;

                    INSERT INTO invoice_items (id, invoice_id, service_type_id, meter_reading_id, item_type, description, quantity, unit_price, amount, created_at)
                    VALUES (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-BLOCK'), blocking_invoice_id, trash_service_id, NULL, 'Service', 'Phí vệ sinh còn thiếu dùng để demo block chấm dứt hợp đồng', 1, 70000, 70000, TIMESTAMPTZ '2026-07-12 09:00:00Z')
                    ON CONFLICT (id) DO NOTHING;
                END $demo$;
                """);
        }
    }
}
