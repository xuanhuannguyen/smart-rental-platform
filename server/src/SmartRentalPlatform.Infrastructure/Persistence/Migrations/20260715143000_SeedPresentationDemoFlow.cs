using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715143000_SeedPresentationDemoFlow")]
    public partial class SeedPresentationDemoFlow : Migration
    {
        public const string DefaultPassword = "Demo@123456";

        public const string SearchTenantEmail = "nguyenxuanhuan.dev@gmail.com";
        public const string PrimaryLandlordEmail = "nguyenxuanhuan21102005@gmail.com";
        public const string SecondaryLandlordEmail = "xunhuns21@gmail.com";
        public const string ActiveTenantEmail = "hoctienganh4english@gmail.com";
        public const string NewOccupantEmail = "demo.flow.newoccupant@example.com";
        public const string AdminEmail = "admin.demo@example.com";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var passwordHash = Quote(PasswordHash());

            migrationBuilder.Sql($$"""
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
                    password_hash text := {{passwordHash}};
                    province_code_value text := '48';
                    province_name_value text := 'Thành phố Đà Nẵng';
                    ward_code_value text := '20285';
                    ward_name_value text := 'Phường Ngũ Hành Sơn';

                    admin_user_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-ADMIN');
                    search_tenant_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-SEARCH-TENANT');
                    active_tenant_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-ACTIVE-TENANT');
                    primary_landlord_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-PRIMARY-LANDLORD');
                    secondary_landlord_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-SECONDARY-LANDLORD');
                    guest_tenant_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-GUEST-TENANT');
                    new_occupant_user_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-NEW-OCCUPANT');

                    hoa_sen_house_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-HOUSE-HOA-SEN');
                    sunrise_house_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-HOUSE-SUNRISE');
                    pending_house_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-HOUSE-PENDING');

                    room_a101_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-A101');
                    room_a102_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-A102');
                    room_b201_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-B201');
                    room_b202_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-B202');
                    room_s101_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-S101');
                    room_s102_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-S102');
                    room_p101_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-P101');

                    active_request_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-REQUEST-ACTIVE');
                    active_deposit_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-DEPOSIT-ACTIVE');
                    active_contract_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-CONTRACT-ACTIVE');
                    ended_contract_1_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-CONTRACT-ENDED-1');
                    ended_contract_2_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-CONTRACT-ENDED-2');
                    ended_request_1_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-REQUEST-ENDED-1');
                    ended_request_2_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-REQUEST-ENDED-2');
                    ended_deposit_1_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-DEPOSIT-ENDED-1');
                    ended_deposit_2_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-DEPOSIT-ENDED-2');

                    search_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-SEARCH-TENANT');
                    active_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-ACTIVE-TENANT');
                    landlord_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-PRIMARY-LANDLORD');
                    secondary_landlord_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-SECONDARY-LANDLORD');

                    electric_service_id uuid;
                    water_service_id uuid;
                    internet_service_id uuid;
                    trash_service_id uuid;

                    june_paid_invoice_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-202606-PAID');
                    july_wrong_invoice_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-202607-WRONG');
                    july_correct_invoice_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-202607-CORRECT');
                    blocking_invoice_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-BLOCK-TERMINATE');
                    final_invoice_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-FINAL');

                    reading_electric_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-ELECTRIC-202607');
                    reading_water_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-WATER-202607');
                    appendix_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-APPENDIX-ACTIVE-1');
                    review_reply_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-REVIEW-REPLIED');
                    review_reported_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-REVIEW-REPORTED');
                    review_pending_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-REVIEW-PENDING-ADMIN');
                BEGIN
                    SELECT p.code, p.name, w.code, w.name
                    INTO province_code_value, province_name_value, ward_code_value, ward_name_value
                    FROM administrative_wards w
                    JOIN administrative_provinces p ON p.code = w.province_code
                    WHERE w.code = '20285' AND w.is_active = TRUE
                    LIMIT 1;

                    IF ward_code_value IS NULL THEN
                        SELECT p.code, p.name, w.code, w.name
                        INTO province_code_value, province_name_value, ward_code_value, ward_name_value
                        FROM administrative_wards w
                        JOIN administrative_provinces p ON p.code = w.province_code
                        WHERE w.is_active = TRUE AND p.is_active = TRUE
                        ORDER BY p.code, w.code
                        LIMIT 1;
                    END IF;

                    IF ward_code_value IS NULL THEN
                        RAISE EXCEPTION 'DEMO-FLOW seed requires at least one active administrative ward.';
                    END IF;

                    -- Reset only the deterministic presentation-demo slice.
                    DELETE FROM review_reports WHERE rooming_house_review_id IN (
                        SELECT id FROM rooming_house_reviews WHERE id IN (review_reply_id, review_reported_id, review_pending_id)
                    );
                    DELETE FROM property_images WHERE object_key LIKE 'demo-flow/%';
                    DELETE FROM rooming_house_reviews WHERE id IN (review_reply_id, review_reported_id, review_pending_id);

                    DELETE FROM chat_messages WHERE conversation_id IN (
                        SELECT id FROM conversations WHERE title LIKE 'DEMO-FLOW:%'
                    );
                    DELETE FROM conversation_participants WHERE conversation_id IN (
                        SELECT id FROM conversations WHERE title LIKE 'DEMO-FLOW:%'
                    );
                    DELETE FROM conversations WHERE title LIKE 'DEMO-FLOW:%';

                    DELETE FROM invoice_items WHERE invoice_id IN (
                        SELECT id FROM invoices WHERE invoice_no LIKE 'DEMO-FLOW-%'
                    );
                    DELETE FROM meter_readings WHERE id IN (reading_electric_id, reading_water_id);
                    DELETE FROM invoices
                    WHERE invoice_no LIKE 'DEMO-FLOW-%'
                       OR id IN (june_paid_invoice_id, july_wrong_invoice_id, july_correct_invoice_id, blocking_invoice_id, final_invoice_id);

                    DELETE FROM contract_signatures WHERE contract_id IN (
                        SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%'
                    ) OR contract_signatures.appendix_id = pg_temp.demo_flow_uuid('DEMO-FLOW-APPENDIX-ACTIVE-1');
                    DELETE FROM contract_files WHERE storage_object_key LIKE 'demo-flow/%';
                    DELETE FROM contract_appendix_changes WHERE contract_appendix_changes.appendix_id = pg_temp.demo_flow_uuid('DEMO-FLOW-APPENDIX-ACTIVE-1');
                    DELETE FROM contract_appendices WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-APPENDIX-ACTIVE-1');
                    DELETE FROM contract_occupant_documents WHERE contract_occupant_id IN (
                        SELECT id FROM contract_occupants WHERE contract_id IN (
                            SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%'
                        )
                    );
                    DELETE FROM contract_occupants WHERE contract_id IN (
                        SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%'
                    );
                    DELETE FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%';
                    DELETE FROM room_deposits WHERE id IN (active_deposit_id, ended_deposit_1_id, ended_deposit_2_id);
                    DELETE FROM rental_requests WHERE id IN (active_request_id, ended_request_1_id, ended_request_2_id)
                        OR tenant_note LIKE 'DEMO-FLOW:%';

                    DELETE FROM viewing_appointments WHERE tenant_note LIKE 'DEMO-FLOW:%';
                    DELETE FROM favorite_rooming_houses WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM withdrawal_requests WHERE idempotency_key LIKE 'demo-flow:%';
                    DELETE FROM payment_transactions WHERE idempotency_key LIKE 'demo-flow:%';
                    DELETE FROM wallet_transactions WHERE description LIKE 'DEMO-FLOW:%'
                        OR wallet_account_id IN (search_wallet_id, active_wallet_id, landlord_wallet_id, secondary_landlord_wallet_id);
                    DELETE FROM wallet_accounts WHERE id IN (search_wallet_id, active_wallet_id, landlord_wallet_id, secondary_landlord_wallet_id);

                    DELETE FROM room_amenities WHERE room_id IN (room_a101_id, room_a102_id, room_b201_id, room_b202_id, room_s101_id, room_s102_id, room_p101_id);
                    DELETE FROM room_price_tiers WHERE room_id IN (room_a101_id, room_a102_id, room_b201_id, room_b202_id, room_s101_id, room_s102_id, room_p101_id);
                    DELETE FROM rooms WHERE id IN (room_a101_id, room_a102_id, room_b201_id, room_b202_id, room_s101_id, room_s102_id, room_p101_id);
                    DELETE FROM rooming_house_service_prices WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_house_rules WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rental_policies WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_house_amenities WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_house_legal_documents WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_houses WHERE id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);

                    INSERT INTO users (id, email, normalized_email, phone_number, password_hash, display_name, avatar_url, status, onboarding_status, email_confirmed, phone_confirmed, access_failed_count, lockout_end_at, last_login_at, created_at, updated_at, deleted_at)
                    VALUES
                        (admin_user_id, 'admin.demo@example.com', 'ADMIN.DEMO@EXAMPLE.COM', '0901000000', password_hash, 'Admin Demo Flow', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL),
                        (search_tenant_id, 'nguyenxuanhuan.dev@gmail.com', 'NGUYENXUANHUAN.DEV@GMAIL.COM', '0901000001', password_hash, 'Nguyễn Xuân Huân - Tenant Demo', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL),
                        (primary_landlord_id, 'nguyenxuanhuan21102005@gmail.com', 'NGUYENXUANHUAN21102005@GMAIL.COM', '0901000002', password_hash, 'Nguyễn Xuân Huân - Chủ trọ Xuân Huân', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL),
                        (secondary_landlord_id, 'xunhuns21@gmail.com', 'XUNHUNS21@GMAIL.COM', '0901000003', password_hash, 'Xuân Huns - Chủ trọ Sunrise', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL),
                        (active_tenant_id, 'hoctienganh4english@gmail.com', 'HOCTIENGANH4ENGLISH@GMAIL.COM', '0901000004', password_hash, 'Lê Quang Linh', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL),
                        (guest_tenant_id, 'demo.flow.guest@example.com', 'DEMO.FLOW.GUEST@EXAMPLE.COM', '0901000005', password_hash, 'Khách Thuê Demo Phụ', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL),
                        (new_occupant_user_id, 'demo.flow.newoccupant@example.com', 'DEMO.FLOW.NEWOCCUPANT@EXAMPLE.COM', '0901000006', password_hash, 'Người Ở Mới Demo', NULL, 'Active', 'Completed', TRUE, FALSE, 0, NULL, seeded_at, seeded_at, now_utc, NULL)
                    ON CONFLICT (normalized_email) DO UPDATE SET
                        email = EXCLUDED.email,
                        phone_number = EXCLUDED.phone_number,
                        password_hash = EXCLUDED.password_hash,
                        display_name = EXCLUDED.display_name,
                        status = 'Active',
                        onboarding_status = 'Completed',
                        email_confirmed = TRUE,
                        updated_at = now_utc,
                        deleted_at = NULL;

                    SELECT id INTO admin_user_id FROM users WHERE normalized_email = 'ADMIN.DEMO@EXAMPLE.COM';
                    SELECT id INTO search_tenant_id FROM users WHERE normalized_email = 'NGUYENXUANHUAN.DEV@GMAIL.COM';
                    SELECT id INTO primary_landlord_id FROM users WHERE normalized_email = 'NGUYENXUANHUAN21102005@GMAIL.COM';
                    SELECT id INTO secondary_landlord_id FROM users WHERE normalized_email = 'XUNHUNS21@GMAIL.COM';
                    SELECT id INTO active_tenant_id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM';
                    SELECT id INTO guest_tenant_id FROM users WHERE normalized_email = 'DEMO.FLOW.GUEST@EXAMPLE.COM';
                    SELECT id INTO new_occupant_user_id FROM users WHERE normalized_email = 'DEMO.FLOW.NEWOCCUPANT@EXAMPLE.COM';

                    DELETE FROM chat_messages
                    WHERE conversation_id IN (
                        SELECT id FROM conversations
                        WHERE type = 'Direct'
                          AND (
                            (direct_user_a_id = LEAST(search_tenant_id, primary_landlord_id) AND direct_user_b_id = GREATEST(search_tenant_id, primary_landlord_id))
                            OR (direct_user_a_id = LEAST(active_tenant_id, primary_landlord_id) AND direct_user_b_id = GREATEST(active_tenant_id, primary_landlord_id))
                          )
                    );
                    DELETE FROM conversation_participants
                    WHERE conversation_id IN (
                        SELECT id FROM conversations
                        WHERE type = 'Direct'
                          AND (
                            (direct_user_a_id = LEAST(search_tenant_id, primary_landlord_id) AND direct_user_b_id = GREATEST(search_tenant_id, primary_landlord_id))
                            OR (direct_user_a_id = LEAST(active_tenant_id, primary_landlord_id) AND direct_user_b_id = GREATEST(active_tenant_id, primary_landlord_id))
                          )
                    );
                    DELETE FROM conversations
                    WHERE type = 'Direct'
                      AND (
                        (direct_user_a_id = LEAST(search_tenant_id, primary_landlord_id) AND direct_user_b_id = GREATEST(search_tenant_id, primary_landlord_id))
                        OR (direct_user_a_id = LEAST(active_tenant_id, primary_landlord_id) AND direct_user_b_id = GREATEST(active_tenant_id, primary_landlord_id))
                      );

                    DELETE FROM withdrawal_webhook_logs
                    WHERE withdrawal_request_id IN (
                        SELECT id FROM withdrawal_requests
                        WHERE wallet_account_id IN (
                            SELECT id FROM wallet_accounts
                            WHERE user_id IN (search_tenant_id, active_tenant_id, primary_landlord_id, secondary_landlord_id)
                        )
                    );
                    DELETE FROM payment_webhook_logs
                    WHERE payment_transaction_id IN (
                        SELECT id FROM payment_transactions
                        WHERE wallet_account_id IN (
                            SELECT id FROM wallet_accounts
                            WHERE user_id IN (search_tenant_id, active_tenant_id, primary_landlord_id, secondary_landlord_id)
                        )
                    );
                    DELETE FROM withdrawal_requests
                    WHERE wallet_account_id IN (
                        SELECT id FROM wallet_accounts
                        WHERE user_id IN (search_tenant_id, active_tenant_id, primary_landlord_id, secondary_landlord_id)
                    );
                    DELETE FROM payment_transactions
                    WHERE wallet_account_id IN (
                        SELECT id FROM wallet_accounts
                        WHERE user_id IN (search_tenant_id, active_tenant_id, primary_landlord_id, secondary_landlord_id)
                    );
                    DELETE FROM wallet_transactions
                    WHERE wallet_account_id IN (
                        SELECT id FROM wallet_accounts
                        WHERE user_id IN (search_tenant_id, active_tenant_id, primary_landlord_id, secondary_landlord_id)
                    );
                    DELETE FROM wallet_accounts
                    WHERE user_id IN (search_tenant_id, active_tenant_id, primary_landlord_id, secondary_landlord_id);

                    INSERT INTO user_profiles (user_id, full_name, date_of_birth, gender, address_line, verified_citizen_id_masked, emergency_contact_name, emergency_contact_phone, created_at, updated_at)
                    VALUES
                        (admin_user_id, 'Admin Demo Flow', DATE '1990-01-01', 'Other', 'Smart Rental Demo Office', NULL, 'Demo Support', '0999000000', seeded_at, now_utc),
                        (search_tenant_id, 'Nguyễn Xuân Huân - Tenant Demo', DATE '2000-10-21', 'Male', 'Đà Nẵng', '079********101', 'Demo Support', '0999000001', seeded_at, now_utc),
                        (primary_landlord_id, 'Nguyễn Xuân Huân - Chủ trọ Xuân Huân', DATE '1995-10-21', 'Male', 'Đà Nẵng', '079********102', 'Demo Support', '0999000002', seeded_at, now_utc),
                        (secondary_landlord_id, 'Xuân Huns - Chủ trọ Sunrise', DATE '1995-08-08', 'Male', 'Đà Nẵng', '079********103', 'Demo Support', '0999000003', seeded_at, now_utc),
                        (active_tenant_id, 'Lê Quang Linh', DATE '1998-04-12', 'Male', 'Đà Nẵng', '079********104', 'Demo Support', '0999000004', seeded_at, now_utc),
                        (guest_tenant_id, 'Khách Thuê Demo Phụ', DATE '1999-05-05', 'Female', 'Đà Nẵng', '079********105', 'Demo Support', '0999000005', seeded_at, now_utc),
                        (new_occupant_user_id, 'Người Ở Mới Demo', DATE '2001-06-15', 'Female', 'Đà Nẵng', '079********106', 'Demo Support', '0999000006', seeded_at, now_utc)
                    ON CONFLICT (user_id) DO UPDATE SET
                        full_name = EXCLUDED.full_name,
                        date_of_birth = EXCLUDED.date_of_birth,
                        gender = EXCLUDED.gender,
                        address_line = EXCLUDED.address_line,
                        verified_citizen_id_masked = EXCLUDED.verified_citizen_id_masked,
                        updated_at = now_utc;

                    DELETE FROM user_roles
                    WHERE user_id IN (admin_user_id, search_tenant_id, primary_landlord_id, secondary_landlord_id, active_tenant_id, guest_tenant_id, new_occupant_user_id)
                      AND role_id IN (1, 2, 3);
                    INSERT INTO user_roles (user_id, role_id, created_at)
                    VALUES
                        (admin_user_id, 1, seeded_at),
                        (search_tenant_id, 2, seeded_at),
                        (active_tenant_id, 2, seeded_at),
                        (guest_tenant_id, 2, seeded_at),
                        (new_occupant_user_id, 2, seeded_at),
                        (primary_landlord_id, 3, seeded_at),
                        (secondary_landlord_id, 3, seeded_at)
                    ON CONFLICT DO NOTHING;

                    INSERT INTO kyc_verifications (id, user_id, document_type, ekyc_provider, ekyc_session_id, front_image_object_key, back_image_object_key, selfie_image_object_key, selfie_capture_method, ocr_full_name, ocr_citizen_id_masked, citizen_id_hash, document_number_encrypted, ocr_date_of_birth, ocr_gender, ocr_address, ocr_confidence, document_check_result, face_match_score, face_match_result, liveness_result, ekyc_result, ekyc_error_code, ekyc_error_message, risk_level, status, reviewed_by_admin_id, rejected_reason, submitted_at, reviewed_at, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-KYC-SEARCH-TENANT'), search_tenant_id, 'CCCD', 'Vnpt', 'demo-flow-search-tenant', 'demo-flow/kyc/search/front.jpg', 'demo-flow/kyc/search/back.jpg', 'demo-flow/kyc/search/selfie.jpg', 'Upload', 'Nguyễn Xuân Huân - Tenant Demo', '079********101', encode(sha256('demo-flow-search-tenant'::bytea), 'hex'), 'encrypted-demo-flow-101', DATE '2000-10-21', 'Male', 'Đà Nẵng', 0.9900, 'Valid', 0.9800, 'Matched', 'Passed', 'Passed', NULL, NULL, 'Low', 'Approved', admin_user_id, NULL, seeded_at, seeded_at, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-KYC-PRIMARY-LANDLORD'), primary_landlord_id, 'CCCD', 'Vnpt', 'demo-flow-primary-landlord', 'demo-flow/kyc/landlord/front.jpg', 'demo-flow/kyc/landlord/back.jpg', 'demo-flow/kyc/landlord/selfie.jpg', 'Upload', 'Nguyễn Xuân Huân - Chủ trọ Xuân Huân', '079********102', encode(sha256('demo-flow-primary-landlord'::bytea), 'hex'), 'encrypted-demo-flow-102', DATE '1995-10-21', 'Male', 'Đà Nẵng', 0.9900, 'Valid', 0.9800, 'Matched', 'Passed', 'Passed', NULL, NULL, 'Low', 'Approved', admin_user_id, NULL, seeded_at, seeded_at, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-KYC-SECONDARY-LANDLORD'), secondary_landlord_id, 'CCCD', 'Vnpt', 'demo-flow-secondary-landlord', 'demo-flow/kyc/secondary/front.jpg', 'demo-flow/kyc/secondary/back.jpg', 'demo-flow/kyc/secondary/selfie.jpg', 'Upload', 'Xuân Huns - Chủ trọ Sunrise', '079********103', encode(sha256('demo-flow-secondary-landlord'::bytea), 'hex'), 'encrypted-demo-flow-103', DATE '1995-08-08', 'Male', 'Đà Nẵng', 0.9900, 'Valid', 0.9800, 'Matched', 'Passed', 'Passed', NULL, NULL, 'Low', 'Approved', admin_user_id, NULL, seeded_at, seeded_at, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-KYC-ACTIVE-TENANT'), active_tenant_id, 'CCCD', 'Vnpt', 'demo-flow-active-tenant', 'demo-flow/kyc/active/front.jpg', 'demo-flow/kyc/active/back.jpg', 'demo-flow/kyc/active/selfie.jpg', 'Upload', 'Lê Quang Linh', '079********104', encode(sha256('demo-flow-active-tenant'::bytea), 'hex'), 'encrypted-demo-flow-104', DATE '1998-04-12', 'Male', 'Đà Nẵng', 0.9900, 'Valid', 0.9800, 'Matched', 'Passed', 'Passed', NULL, NULL, 'Low', 'Approved', admin_user_id, NULL, seeded_at, seeded_at, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-KYC-NEW-OCCUPANT'), new_occupant_user_id, 'CCCD', 'Vnpt', 'demo-flow-new-occupant', 'demo-flow/kyc/new-occupant/front.jpg', 'demo-flow/kyc/new-occupant/back.jpg', 'demo-flow/kyc/new-occupant/selfie.jpg', 'Upload', 'Người Ở Mới Demo', '079********106', encode(sha256('demo-flow-new-occupant'::bytea), 'hex'), 'encrypted-demo-flow-106', DATE '2001-06-15', 'Female', 'Đà Nẵng', 0.9900, 'Valid', 0.9800, 'Matched', 'Passed', 'Passed', NULL, NULL, 'Low', 'Approved', admin_user_id, NULL, seeded_at, seeded_at, seeded_at, now_utc)
                    ON CONFLICT (id) DO UPDATE SET
                        ocr_full_name = EXCLUDED.ocr_full_name,
                        ocr_citizen_id_masked = EXCLUDED.ocr_citizen_id_masked,
                        ocr_date_of_birth = EXCLUDED.ocr_date_of_birth,
                        ocr_gender = EXCLUDED.ocr_gender,
                        ocr_address = EXCLUDED.ocr_address,
                        status = EXCLUDED.status,
                        ekyc_result = EXCLUDED.ekyc_result,
                        reviewed_by_admin_id = EXCLUDED.reviewed_by_admin_id,
                        reviewed_at = EXCLUDED.reviewed_at,
                        updated_at = now_utc;

                    SELECT id INTO electric_service_id FROM billing_service_types WHERE lower(name) = lower('Điện') LIMIT 1;
                    IF electric_service_id IS NULL THEN
                        electric_service_id := pg_temp.demo_flow_uuid('DEMO-FLOW-SERVICE-ELECTRIC');
                        INSERT INTO billing_service_types (id, name, supports_meter_reading, meter_unit_name, is_active, created_at)
                        VALUES (electric_service_id, 'Điện', TRUE, 'kWh', TRUE, seeded_at)
                        ON CONFLICT (name) DO NOTHING;
                    END IF;
                    SELECT id INTO water_service_id FROM billing_service_types WHERE lower(name) = lower('Nước') LIMIT 1;
                    IF water_service_id IS NULL THEN
                        water_service_id := pg_temp.demo_flow_uuid('DEMO-FLOW-SERVICE-WATER');
                        INSERT INTO billing_service_types (id, name, supports_meter_reading, meter_unit_name, is_active, created_at)
                        VALUES (water_service_id, 'Nước', TRUE, 'm3', TRUE, seeded_at)
                        ON CONFLICT (name) DO NOTHING;
                    END IF;
                    SELECT id INTO internet_service_id FROM billing_service_types WHERE lower(name) IN (lower('Internet'), lower('Wifi')) LIMIT 1;
                    IF internet_service_id IS NULL THEN
                        internet_service_id := pg_temp.demo_flow_uuid('DEMO-FLOW-SERVICE-INTERNET');
                        INSERT INTO billing_service_types (id, name, supports_meter_reading, meter_unit_name, is_active, created_at)
                        VALUES (internet_service_id, 'Internet', FALSE, NULL, TRUE, seeded_at)
                        ON CONFLICT (name) DO NOTHING;
                    END IF;
                    SELECT id INTO trash_service_id FROM billing_service_types WHERE lower(name) = lower('Rác') LIMIT 1;
                    IF trash_service_id IS NULL THEN
                        trash_service_id := pg_temp.demo_flow_uuid('DEMO-FLOW-SERVICE-TRASH');
                        INSERT INTO billing_service_types (id, name, supports_meter_reading, meter_unit_name, is_active, created_at)
                        VALUES (trash_service_id, 'Rác', FALSE, NULL, TRUE, seeded_at)
                        ON CONFLICT (name) DO NOTHING;
                    END IF;

                    INSERT INTO rooming_houses (id, landlord_user_id, name, description, address_line, ward_code, province_code, address_display, latitude, longitude, google_map_url, approval_status, visibility_status, average_rating, total_reviews, rejected_reason, reviewed_by_admin_id, reviewed_at, created_at, updated_at, deleted_at)
                    VALUES
                        (hoa_sen_house_id, primary_landlord_id, 'Khu trọ Xuân Huân', 'Khu trọ chính cho demo: phòng A01 còn trống để tenant đầu tiên thuê, phòng B201 đang có hợp đồng active của Lê Quang Linh, các phòng còn lại có lịch sử thuê và hóa đơn doanh thu các tháng trước.', '144 Trần Đại Nghĩa', ward_code_value, province_code_value, '144 Trần Đại Nghĩa, ' || ward_name_value || ', ' || province_name_value, 15.9754000, 108.2638000, 'https://maps.example/demo-flow/xuan-huan', 'Approved', 'Visible', 4.5, 2, NULL, admin_user_id, seeded_at, seeded_at, now_utc, NULL),
                        (sunrise_house_id, secondary_landlord_id, 'Nhà trọ Sunrise Demo', 'Khu trọ phụ để dashboard chủ trọ có thêm lịch xem phòng, hóa đơn, review và dữ liệu tìm kiếm.', '88 Đường Số 7', ward_code_value, province_code_value, '88 Đường Số 7, ' || ward_name_value || ', ' || province_name_value, 15.9800000, 108.2650000, 'https://maps.example/demo-flow/sunrise', 'Approved', 'Visible', 4.0, 1, NULL, admin_user_id, seeded_at, seeded_at, now_utc, NULL),
                        (pending_house_id, primary_landlord_id, 'Nhà trọ Garden Pending Demo', 'Hồ sơ khu trọ đang chờ admin duyệt để demo luồng admin duyệt khu trọ.', '36 Nguyễn Hữu Thọ', ward_code_value, province_code_value, '36 Nguyễn Hữu Thọ, ' || ward_name_value || ', ' || province_name_value, 15.9820000, 108.2680000, 'https://maps.example/demo-flow/garden-pending', 'Pending', 'Hidden', 0, 0, NULL, NULL, NULL, seeded_at, now_utc, NULL)
                    ON CONFLICT (id) DO UPDATE SET
                        landlord_user_id = EXCLUDED.landlord_user_id,
                        name = EXCLUDED.name,
                        description = EXCLUDED.description,
                        approval_status = EXCLUDED.approval_status,
                        visibility_status = EXCLUDED.visibility_status,
                        average_rating = EXCLUDED.average_rating,
                        total_reviews = EXCLUDED.total_reviews,
                        updated_at = now_utc,
                        deleted_at = NULL;

                    INSERT INTO rental_policies (id, rooming_house_id, min_rental_months, max_rental_months, allow_short_term_renewal, renewal_notice_days, deposit_months, default_payment_day, is_active, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-POLICY-HOA-SEN'), hoa_sen_house_id, 3, 12, TRUE, 30, 1.00, 5, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-POLICY-SUNRISE'), sunrise_house_id, 6, 12, TRUE, 30, 1.00, 5, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-POLICY-PENDING'), pending_house_id, 3, 12, TRUE, 30, 1.00, 5, TRUE, seeded_at, now_utc)
                    ON CONFLICT (rooming_house_id) DO UPDATE SET
                        min_rental_months = EXCLUDED.min_rental_months,
                        max_rental_months = EXCLUDED.max_rental_months,
                        deposit_months = EXCLUDED.deposit_months,
                        default_payment_day = EXCLUDED.default_payment_day,
                        updated_at = now_utc;

                    INSERT INTO rooming_house_rules (id, rooming_house_id, source_type, pdf_object_key, general_rules, quiet_hours, security_policy, cleaning_policy, guest_policy, parking_policy, utility_policy, damage_compensation_policy, additional_notes, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-RULE-HOA-SEN'), hoa_sen_house_id, 'FormGenerated', 'demo-flow/rules/hoa-sen.pdf', 'Giữ vệ sinh chung, không gây mất trật tự, không tự ý chuyển nhượng phòng.', '22:00-06:00', 'Khách qua đêm phải báo trước với chủ trọ.', 'Đổ rác đúng nơi quy định, dọn khu vực chung sau khi sử dụng.', 'Khách ở lại qua đêm cần đăng ký thông tin.', 'Để xe đúng vị trí, tự bảo quản tài sản cá nhân.', 'Điện nước tính theo chỉ số, dịch vụ cố định hiển thị trong bảng giá.', 'Bồi thường theo thiệt hại thực tế nếu làm hỏng tài sản.', 'Nội quy dùng cho demo đọc luật và chính sách thuê.', seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-RULE-SUNRISE'), sunrise_house_id, 'FormGenerated', 'demo-flow/rules/sunrise.pdf', 'Khu trọ an ninh, ưu tiên sinh viên và nhân viên văn phòng.', '22:30-06:00', 'Camera khu vực chung, không giao chìa khóa cho người ngoài.', 'Giữ sạch hành lang và khu giặt phơi.', 'Khách qua đêm báo trước 24 giờ.', 'Có khu để xe riêng.', 'Điện nước theo chỉ số, internet cố định hằng tháng.', 'Bồi thường theo biên bản nếu phát sinh hư hỏng.', 'Nội quy phụ để demo nhiều khu trọ.', seeded_at, now_utc)
                    ON CONFLICT (rooming_house_id) DO UPDATE SET
                        general_rules = EXCLUDED.general_rules,
                        quiet_hours = EXCLUDED.quiet_hours,
                        utility_policy = EXCLUDED.utility_policy,
                        updated_at = now_utc;

                    INSERT INTO rooming_house_legal_documents (rooming_house_id, document_type, front_image_object_key, back_image_object_key, extra_image_object_key, document_number_masked, document_number_hash, uploaded_at, created_at, updated_at)
                    VALUES
                        (hoa_sen_house_id, 'LAND_USE_CERTIFICATE', 'demo-flow/legal/hoa-sen-front.jpg', 'demo-flow/legal/hoa-sen-back.jpg', NULL, 'HS****2026', encode(sha256('demo-flow-hoa-sen-legal'::bytea), 'hex'), seeded_at, seeded_at, now_utc),
                        (sunrise_house_id, 'LAND_USE_CERTIFICATE', 'demo-flow/legal/sunrise-front.jpg', 'demo-flow/legal/sunrise-back.jpg', NULL, 'SR****2026', encode(sha256('demo-flow-sunrise-legal'::bytea), 'hex'), seeded_at, seeded_at, now_utc),
                        (pending_house_id, 'LAND_USE_CERTIFICATE', 'demo-flow/legal/pending-front.jpg', 'demo-flow/legal/pending-back.jpg', NULL, 'GP****2026', encode(sha256('demo-flow-pending-legal'::bytea), 'hex'), seeded_at, seeded_at, now_utc)
                    ON CONFLICT (rooming_house_id) DO UPDATE SET
                        front_image_object_key = EXCLUDED.front_image_object_key,
                        back_image_object_key = EXCLUDED.back_image_object_key,
                        updated_at = now_utc;

                    INSERT INTO rooms (id, rooming_house_id, room_number, floor, area_m2, max_occupants, is_tiered_pricing, status, description, created_at, updated_at, deleted_at)
                    VALUES
                        (room_a101_id, hoa_sen_house_id, 'A01', 1, 24.00, 2, TRUE, 'Available', 'Phòng A01 còn trống duy nhất cho account tenant đầu tiên tìm kiếm, đặt lịch, thuê và ký hợp đồng mới; giá thay đổi theo 1 hoặc 2 người ở.', seeded_at, now_utc, NULL),
                        (room_a102_id, hoa_sen_house_id, 'A02', 1, 22.00, 2, TRUE, 'Maintenance', 'Phòng A02 đã có lịch sử thuê đủ kỳ hóa đơn trước demo, hiện tạm bảo trì nên không xuất hiện trong luồng thuê mới.', seeded_at, now_utc, NULL),
                        (room_b201_id, hoa_sen_house_id, 'B201', 2, 26.00, 2, TRUE, 'Occupied', 'Phòng B201 cho tối đa 2 người; hợp đồng demo hiện tại của Lê Quang Linh chỉ có 1 người ở.', seeded_at, now_utc, NULL),
                        (room_b202_id, hoa_sen_house_id, 'B202', 2, 20.00, 1, FALSE, 'Maintenance', 'Phòng B202 có hợp đồng cũ và hóa đơn doanh thu 3 tháng trước, hiện đã trả phòng.', seeded_at, now_utc, NULL),
                        (room_s101_id, sunrise_house_id, 'S101', 1, 23.00, 2, TRUE, 'Available', 'Phòng Sunrise còn trống cho dashboard và search.', seeded_at, now_utc, NULL),
                        (room_s102_id, sunrise_house_id, 'S102', 1, 25.00, 3, TRUE, 'Occupied', 'Phòng Sunrise đã thuê để tạo review/report.', seeded_at, now_utc, NULL),
                        (room_p101_id, pending_house_id, 'P101', 1, 21.00, 2, TRUE, 'Hidden', 'Phòng thuộc khu trọ pending để admin duyệt.', seeded_at, now_utc, NULL)
                    ON CONFLICT (id) DO UPDATE SET
                        status = EXCLUDED.status,
                        description = EXCLUDED.description,
                        updated_at = now_utc,
                        deleted_at = NULL;

                    INSERT INTO room_price_tiers (id, room_id, occupant_count, monthly_rent, is_active, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-A101-1'), room_a101_id, 1, 3500000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-A101-2'), room_a101_id, 2, 3900000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-A102-1'), room_a102_id, 1, 3200000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-A102-2'), room_a102_id, 2, 3600000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-B201-1'), room_b201_id, 1, 3600000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-B201-2'), room_b201_id, 2, 3950000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-B202-1'), room_b202_id, 1, 3000000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-S101-1'), room_s101_id, 1, 3300000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-S101-2'), room_s101_id, 2, 3700000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-S102-1'), room_s102_id, 1, 3400000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-S102-2'), room_s102_id, 2, 3900000, TRUE, seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-P101-1'), room_p101_id, 1, 3100000, TRUE, seeded_at, now_utc)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO rooming_house_service_prices (id, rooming_house_id, service_type_id, pricing_unit, unit_price, effective_from, effective_to, is_active, note, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PRICE-HOA-ELECTRIC'), hoa_sen_house_id, electric_service_id, 'MeterReading', 4000, DATE '2026-01-01', NULL, TRUE, 'Điện theo chỉ số, dùng demo AI đọc ảnh.', seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PRICE-HOA-WATER'), hoa_sen_house_id, water_service_id, 'MeterReading', 18000, DATE '2026-01-01', NULL, TRUE, 'Nước theo chỉ số, dùng demo AI đọc ảnh.', seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PRICE-HOA-INTERNET'), hoa_sen_house_id, internet_service_id, 'PerMonth', 100000, DATE '2026-01-01', NULL, TRUE, 'Internet cố định hằng tháng.', seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PRICE-HOA-TRASH'), hoa_sen_house_id, trash_service_id, 'PerPersonPerMonth', 30000, DATE '2026-01-01', NULL, TRUE, 'Rác theo số người ở.', seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PRICE-SUN-ELECTRIC'), sunrise_house_id, electric_service_id, 'MeterReading', 4200, DATE '2026-01-01', NULL, TRUE, 'Điện theo chỉ số.', seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PRICE-SUN-WATER'), sunrise_house_id, water_service_id, 'MeterReading', 17000, DATE '2026-01-01', NULL, TRUE, 'Nước theo chỉ số.', seeded_at, now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PRICE-SUN-INTERNET'), sunrise_house_id, internet_service_id, 'PerMonth', 120000, DATE '2026-01-01', NULL, TRUE, 'Internet cố định hằng tháng.', seeded_at, now_utc)
                    ON CONFLICT (rooming_house_id, service_type_id, effective_from) DO UPDATE SET
                        pricing_unit = EXCLUDED.pricing_unit,
                        unit_price = EXCLUDED.unit_price,
                        note = EXCLUDED.note,
                        is_active = TRUE,
                        updated_at = now_utc;

                    INSERT INTO rooming_house_amenities (rooming_house_id, amenity_id)
                    SELECT hoa_sen_house_id, id FROM amenities WHERE id IN (1, 2, 3, 5, 7)
                    ON CONFLICT DO NOTHING;
                    INSERT INTO rooming_house_amenities (rooming_house_id, amenity_id)
                    SELECT sunrise_house_id, id FROM amenities WHERE id IN (1, 3, 4, 6)
                    ON CONFLICT DO NOTHING;
                    INSERT INTO room_amenities (room_id, amenity_id)
                    SELECT room_a101_id, id FROM amenities WHERE id IN (1, 5, 7)
                    ON CONFLICT DO NOTHING;
                    INSERT INTO room_amenities (room_id, amenity_id)
                    SELECT room_b201_id, id FROM amenities WHERE id IN (1, 5, 7)
                    ON CONFLICT DO NOTHING;
                    INSERT INTO room_amenities (room_id, amenity_id)
                    SELECT room_s101_id, id FROM amenities WHERE id IN (1, 6)
                    ON CONFLICT DO NOTHING;

                    INSERT INTO property_images (id, rooming_house_id, room_id, rooming_house_review_id, object_key, image_url, caption, is_cover, sort_order, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-HOA-1'), hoa_sen_house_id, NULL, NULL, 'demo-flow/houses/hoa-sen/cover.jpg', '/uploads/demo-flow/houses/hoa-sen/cover.jpg', 'Mặt tiền Khu trọ Xuân Huân', TRUE, 1, seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-HOA-2'), hoa_sen_house_id, NULL, NULL, 'demo-flow/houses/hoa-sen/corridor.jpg', '/uploads/demo-flow/houses/hoa-sen/corridor.jpg', 'Hành lang sạch sẽ', FALSE, 2, seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-HOA-3'), hoa_sen_house_id, NULL, NULL, 'demo-flow/houses/hoa-sen/parking.jpg', '/uploads/demo-flow/houses/hoa-sen/parking.jpg', 'Khu để xe', FALSE, 3, seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-SUN-1'), sunrise_house_id, NULL, NULL, 'demo-flow/houses/sunrise/cover.jpg', '/uploads/demo-flow/houses/sunrise/cover.jpg', 'Mặt tiền Sunrise Demo', TRUE, 1, seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-A101-1'), NULL, room_a101_id, NULL, 'demo-flow/rooms/a101/cover.jpg', '/uploads/demo-flow/rooms/a101/cover.jpg', 'Phòng A01', TRUE, 1, seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-B201-1'), NULL, room_b201_id, NULL, 'demo-flow/rooms/b201/cover.jpg', '/uploads/demo-flow/rooms/b201/cover.jpg', 'Phòng B201', TRUE, 1, seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-S101-1'), NULL, room_s101_id, NULL, 'demo-flow/rooms/s101/cover.jpg', '/uploads/demo-flow/rooms/s101/cover.jpg', 'Phòng S101', TRUE, 1, seeded_at)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO rental_requests (id, room_id, tenant_user_id, approved_by_landlord_id, desired_start_date, expected_end_date, expected_occupant_count, monthly_rent_snapshot, deposit_amount_snapshot, tenant_note, status, responded_at, rejected_reason, created_at, updated_at)
                    VALUES
                        (active_request_id, room_b201_id, active_tenant_id, primary_landlord_id, DATE '2026-05-01', DATE '2027-04-30', 1, 3600000, 3600000, 'Yêu cầu thuê phòng B201 cho 1 người ở, bắt đầu từ tháng 05/2026.', 'Accepted', TIMESTAMPTZ '2026-04-25 09:00:00Z', NULL, TIMESTAMPTZ '2026-04-24 09:00:00Z', now_utc),
                        (ended_request_1_id, room_b202_id, guest_tenant_id, primary_landlord_id, DATE '2026-02-01', DATE '2026-04-30', 1, 3000000, 3000000, 'DEMO-FLOW: request cũ của tenant phụ để tạo review đã trả lời.', 'Accepted', TIMESTAMPTZ '2026-01-25 09:00:00Z', NULL, TIMESTAMPTZ '2026-01-24 09:00:00Z', now_utc),
                        (ended_request_2_id, room_s102_id, guest_tenant_id, secondary_landlord_id, DATE '2026-02-01', DATE '2026-04-30', 2, 3900000, 3900000, 'DEMO-FLOW: request cũ để tạo review bị report.', 'Accepted', TIMESTAMPTZ '2026-01-26 09:00:00Z', NULL, TIMESTAMPTZ '2026-01-24 10:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-REQUEST-PENDING-DASHBOARD'), room_s101_id, guest_tenant_id, NULL, DATE '2026-08-01', DATE '2027-01-31', 1, 3300000, 3300000, 'DEMO-FLOW: pending rental request cho dashboard landlord.', 'Pending', NULL, NULL, TIMESTAMPTZ '2026-07-12 09:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-REQUEST-REJECTED-DASHBOARD'), room_a102_id, guest_tenant_id, primary_landlord_id, DATE '2026-08-01', DATE '2027-01-31', 1, 3200000, 3200000, 'DEMO-FLOW: rejected rental request cho dashboard landlord.', 'Rejected', TIMESTAMPTZ '2026-07-13 09:00:00Z', 'Phòng đã có khách hẹn xem trước.', TIMESTAMPTZ '2026-07-12 10:00:00Z', now_utc)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO room_deposits (id, rental_request_id, room_id, tenant_user_id, landlord_user_id, deposit_amount, currency, status, payment_deadline_at, paid_at, refunded_at, forfeited_at, refund_amount, forfeited_amount, note, payment_transfer_group_id, refund_transfer_group_id, created_at, updated_at)
                    VALUES
                        (active_deposit_id, active_request_id, room_b201_id, active_tenant_id, primary_landlord_id, 3600000, 'VND', 'Paid', TIMESTAMPTZ '2026-04-28 23:59:00Z', TIMESTAMPTZ '2026-04-25 10:00:00Z', NULL, NULL, NULL, NULL, 'Tiền cọc phòng B201 đã thanh toán. Nếu chấm dứt trước hạn, tiền cọc được chuyển cho chủ trọ theo điều khoản hợp đồng.', pg_temp.demo_flow_uuid('DEMO-FLOW-TG-DEPOSIT-ACTIVE'), NULL, TIMESTAMPTZ '2026-04-25 09:00:00Z', now_utc),
                        (ended_deposit_1_id, ended_request_1_id, room_b202_id, guest_tenant_id, primary_landlord_id, 3000000, 'VND', 'Refunded', TIMESTAMPTZ '2026-01-28 23:59:00Z', TIMESTAMPTZ '2026-01-25 10:00:00Z', TIMESTAMPTZ '2026-04-30 10:00:00Z', NULL, 3000000, NULL, 'DEMO-FLOW: cọc hợp đồng cũ đã hoàn.', pg_temp.demo_flow_uuid('DEMO-FLOW-TG-DEPOSIT-ENDED-1'), pg_temp.demo_flow_uuid('DEMO-FLOW-TG-REFUND-ENDED-1'), TIMESTAMPTZ '2026-01-25 09:00:00Z', now_utc),
                        (ended_deposit_2_id, ended_request_2_id, room_s102_id, guest_tenant_id, secondary_landlord_id, 3900000, 'VND', 'Refunded', TIMESTAMPTZ '2026-01-28 23:59:00Z', TIMESTAMPTZ '2026-01-26 10:00:00Z', TIMESTAMPTZ '2026-04-30 10:00:00Z', NULL, 3900000, NULL, 'DEMO-FLOW: cọc hợp đồng cũ Sunrise đã hoàn.', pg_temp.demo_flow_uuid('DEMO-FLOW-TG-DEPOSIT-ENDED-2'), pg_temp.demo_flow_uuid('DEMO-FLOW-TG-REFUND-ENDED-2'), TIMESTAMPTZ '2026-01-26 09:00:00Z', now_utc)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO contracts (id, rental_request_id, room_deposit_id, room_id, main_tenant_user_id, contract_number, start_date, end_date, monthly_rent, deposit_amount, payment_day, status, room_snapshot, signature_deadline_at, activated_at, termination_date, termination_type, status_reason, created_at, updated_at, deleted_at)
                    VALUES
                        (active_contract_id, active_request_id, active_deposit_id, room_b201_id, active_tenant_id, 'DEMO-FLOW-ACTIVE-B201-20260601', DATE '2026-05-01', DATE '2027-04-30', 3600000, 3600000, 5, 'Active', '{"RoomNumber":"B201","RoomingHouseName":"Khu trọ Xuân Huân","MaxOccupants":2,"OccupantCount":1}'::jsonb, NULL, TIMESTAMPTZ '2026-04-25 15:00:00Z', NULL, NULL, NULL, TIMESTAMPTZ '2026-04-25 09:00:00Z', now_utc, NULL),
                        (ended_contract_1_id, ended_request_1_id, ended_deposit_1_id, room_b202_id, guest_tenant_id, 'DEMO-FLOW-ENDED-B202-20260201', DATE '2026-02-01', DATE '2026-04-30', 3000000, 3000000, 5, 'Expired', '{"RoomNumber":"B202","RoomingHouseName":"Khu trọ Xuân Huân","MaxOccupants":1}'::jsonb, NULL, TIMESTAMPTZ '2026-01-25 15:00:00Z', DATE '2026-04-30', 'NormalExpiration', 'Hợp đồng cũ của tenant phụ dùng để tạo review và doanh thu 3 tháng trước.', TIMESTAMPTZ '2026-01-25 09:00:00Z', now_utc, NULL),
                        (ended_contract_2_id, ended_request_2_id, ended_deposit_2_id, room_s102_id, guest_tenant_id, 'DEMO-FLOW-ENDED-S102-20260201', DATE '2026-02-01', DATE '2026-04-30', 3900000, 3900000, 5, 'Expired', '{"RoomNumber":"S102","RoomingHouseName":"Nhà trọ Sunrise Demo","MaxOccupants":2}'::jsonb, NULL, TIMESTAMPTZ '2026-01-26 15:00:00Z', DATE '2026-04-30', 'NormalExpiration', 'Hợp đồng cũ dùng để tạo review/report.', TIMESTAMPTZ '2026-01-26 09:00:00Z', now_utc, NULL)
                    ON CONFLICT (contract_number) DO NOTHING;

                    INSERT INTO contract_occupants (id, contract_id, user_id, guardian_occupant_id, full_name, phone_number, date_of_birth, relationship_to_main_tenant, move_in_date, move_out_date, status, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-OCC-ACTIVE-MAIN'), active_contract_id, active_tenant_id, NULL, 'Lê Quang Linh', '0901000004', DATE '1998-04-12', 'Self', DATE '2026-05-01', NULL, 'Active', TIMESTAMPTZ '2026-04-25 15:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-OCC-ENDED-1'), ended_contract_1_id, guest_tenant_id, NULL, 'Khách Thuê Demo Phụ', '0901000005', DATE '1999-05-05', 'Self', DATE '2026-02-01', DATE '2026-04-30', 'MoveOut', TIMESTAMPTZ '2026-01-25 15:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-OCC-ENDED-2'), ended_contract_2_id, guest_tenant_id, NULL, 'Khách Thuê Demo Phụ', '0901000005', DATE '1999-05-05', 'Self', DATE '2026-02-01', DATE '2026-04-30', 'MoveOut', TIMESTAMPTZ '2026-01-26 15:00:00Z', now_utc)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO contract_files (id, contract_id, appendix_id, storage_object_key, purpose, content_type, "FileUrl", sha256_hash, is_legally_signed, signing_envelope_id, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-FILE-ACTIVE-SIGNED'), active_contract_id, NULL, 'demo-flow/contracts/active-b201-signed-vnpt.pdf', 'SignedLegalDocument', 'application/pdf', '/uploads/demo-flow/contracts/active-b201-signed-vnpt.pdf', 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce', TRUE, NULL, TIMESTAMPTZ '2026-04-25 15:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-FILE-ACTIVE-PREVIEW'), active_contract_id, NULL, 'demo-flow/contracts/active-b201-preview-vnpt.pdf', 'Preview', 'application/pdf', '/uploads/demo-flow/contracts/active-b201-preview-vnpt.pdf', '58bb3ebd5d963a985b2c5e0522bdc2d4d4ed450552b976c03ebe34821382b8ab', FALSE, NULL, TIMESTAMPTZ '2026-04-25 14:00:00Z')
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO contract_signatures (id, contract_id, appendix_id, signer_user_id, signer_role, signature_method, status, signing_order, signing_envelope_id, provider, provider_envelope_id, provider_participant_id, signing_url, certificate_serial_number, certificate_subject, certificate_issuer, signed_file_sha256_hash, provider_evidence_json, notified_at, signed_at, ip_address, user_agent, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-SIGN-ACTIVE-LANDLORD'), active_contract_id, NULL, primary_landlord_id, 'Landlord', 'VnptSmsOtp', 'Signed', 1, NULL, 'Vnpt', 'VNPT-ECONTRACT-B201-20260425', 'VNPT-PART-LANDLORD-0901000002', NULL, 'VNPT-CA-2026-000102', 'CN=Nguyễn Xuân Huân, O=VNPT SmartCA, C=VN', 'VNPT Certification Authority', 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce', '{"provider":"VNPT eContract","auth_method":"SMS_OTP","otp_delivery":"sms","otp_verified_at":"2026-04-25T14:00:00Z","masked_phone":"090****002","transaction_id":"VNPT-OTP-B201-LANDLORD-20260425","document_number":"HD-202604250900-B201-XUANHUAN"}'::jsonb, TIMESTAMPTZ '2026-04-25 13:45:00Z', TIMESTAMPTZ '2026-04-25 14:00:00Z', '127.0.0.1', 'VNPT eContract Demo Seed', TIMESTAMPTZ '2026-04-25 13:45:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-SIGN-ACTIVE-TENANT'), active_contract_id, NULL, active_tenant_id, 'Tenant', 'VnptSmsOtp', 'Signed', 2, NULL, 'Vnpt', 'VNPT-ECONTRACT-B201-20260425', 'VNPT-PART-TENANT-0901000004', NULL, 'VNPT-CA-2026-000104', 'CN=Lê Quang Linh, O=VNPT SmartCA, C=VN', 'VNPT Certification Authority', 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce', '{"provider":"VNPT eContract","auth_method":"SMS_OTP","otp_delivery":"sms","otp_verified_at":"2026-04-25T15:00:00Z","masked_phone":"090****004","transaction_id":"VNPT-OTP-B201-TENANT-20260425","document_number":"HD-202604250900-B201-XUANHUAN"}'::jsonb, TIMESTAMPTZ '2026-04-25 14:45:00Z', TIMESTAMPTZ '2026-04-25 15:00:00Z', '127.0.0.1', 'VNPT eContract Demo Seed', TIMESTAMPTZ '2026-04-25 14:45:00Z')
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO contract_appendices (id, contract_id, appendix_number, effective_date, status, created_by_user_id, activated_at, applied_at, status_reason, created_at, updated_at)
                    VALUES (appendix_id, active_contract_id, 'PL-DEMO-001', DATE '2026-07-01', 'Active', primary_landlord_id, TIMESTAMPTZ '2026-07-01 09:00:00Z', TIMESTAMPTZ '2026-07-01 09:05:00Z', 'Demo phụ lục điều chỉnh ngày thanh toán và xác nhận người ở.', TIMESTAMPTZ '2026-06-28 09:00:00Z', now_utc)
                    ON CONFLICT (contract_id, appendix_number) DO NOTHING;

                    INSERT INTO contract_appendix_changes (id, appendix_id, change_type, target_type, target_id, field_name, old_value, new_value, sort_order, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPENDIX-CHANGE-PAYDAY'), appendix_id, 'Update', 'Contract', active_contract_id, 'PaymentDay', '5'::jsonb, '10'::jsonb, 1, TIMESTAMPTZ '2026-06-28 09:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPENDIX-CHANGE-NOTE'), appendix_id, 'Update', 'Contract', active_contract_id, 'StatusReason', '"Bổ sung phụ lục demo thông tin người ở."'::jsonb, '"Đã áp dụng phụ lục demo thông tin người ở."'::jsonb, 2, TIMESTAMPTZ '2026-06-28 09:00:00Z')
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO contract_files (id, contract_id, appendix_id, storage_object_key, purpose, content_type, "FileUrl", sha256_hash, is_legally_signed, signing_envelope_id, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-FILE-APPENDIX-SIGNED'), active_contract_id, appendix_id, 'demo-flow/contracts/appendix-001-signed.pdf', 'SignedLegalDocument', 'application/pdf', '/uploads/demo-flow/contracts/appendix-001-signed.pdf', encode(sha256('demo-flow-appendix'::bytea), 'hex'), TRUE, NULL, TIMESTAMPTZ '2026-07-01 09:05:00Z')
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO meter_readings (id, room_id, contract_id, service_type_id, billing_period_start, billing_period_end, previous_reading, current_reading, consumption, proof_image_object_key, ai_reading, ai_raw_text, was_manually_corrected, recorded_by_landlord_user_id, reading_at, created_at, updated_at)
                    VALUES
                        (reading_electric_id, room_b201_id, active_contract_id, electric_service_id, DATE '2026-06-01', DATE '2026-06-30', 1250, 1341, 91, 'demo-flow/meters/b201-electric-202606.png', 1341, 'AI OCR: điện hiện tại 1341 kWh', FALSE, primary_landlord_id, TIMESTAMPTZ '2026-06-30 08:00:00Z', TIMESTAMPTZ '2026-06-30 08:00:00Z', now_utc),
                        (reading_water_id, room_b201_id, active_contract_id, water_service_id, DATE '2026-06-01', DATE '2026-06-30', 88, 96, 8, 'demo-flow/meters/b201-water-202606.png', 96, 'AI OCR: nước hiện tại 96 m3', FALSE, primary_landlord_id, TIMESTAMPTZ '2026-06-30 08:05:00Z', TIMESTAMPTZ '2026-06-30 08:05:00Z', now_utc)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO invoices (id, contract_id, room_id, tenant_user_id, landlord_user_id, invoice_no, billing_period_start, billing_period_end, issue_date, due_date, rent_amount, utility_amount, service_amount, discount_amount, total_amount, status, note, sent_at, paid_at, cancelled_at, cancel_reason, wallet_transfer_group_id, created_at, updated_at)
                    VALUES
                        (june_paid_invoice_id, active_contract_id, room_b201_id, active_tenant_id, primary_landlord_id, 'HD-B201-202605', DATE '2026-05-01', DATE '2026-05-31', DATE '2026-05-31', DATE '2026-06-05', 3600000, 520000, 160000, 0, 4280000, 'Paid', 'Hóa đơn tháng 05/2026 đã thanh toán.', TIMESTAMPTZ '2026-05-31 08:00:00Z', TIMESTAMPTZ '2026-06-01 09:00:00Z', NULL, NULL, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-MAY'), TIMESTAMPTZ '2026-05-31 08:00:00Z', now_utc),
                        (july_correct_invoice_id, active_contract_id, room_b201_id, active_tenant_id, primary_landlord_id, 'HD-B201-202606', DATE '2026-06-01', DATE '2026-06-30', DATE '2026-06-30', DATE '2026-07-05', 3600000, 508000, 120000, 0, 4228000, 'Overdue', 'Hóa đơn tháng 06/2026 đã quá hạn thanh toán.', TIMESTAMPTZ '2026-06-30 09:00:00Z', NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-06-30 09:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202602'), ended_contract_1_id, room_b202_id, guest_tenant_id, primary_landlord_id, 'HD-B202-202602', DATE '2026-02-01', DATE '2026-02-28', DATE '2026-02-28', DATE '2026-03-05', 3000000, 280000, 130000, 0, 3410000, 'Paid', 'Hóa đơn phòng B202 tháng 02/2026 đã thanh toán, dùng cho dashboard doanh thu quá khứ.', TIMESTAMPTZ '2026-02-28 08:00:00Z', TIMESTAMPTZ '2026-03-02 09:00:00Z', NULL, NULL, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-B202-202602'), TIMESTAMPTZ '2026-02-28 08:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202603'), ended_contract_1_id, room_b202_id, guest_tenant_id, primary_landlord_id, 'HD-B202-202603', DATE '2026-03-01', DATE '2026-03-31', DATE '2026-03-31', DATE '2026-04-05', 3000000, 320000, 130000, 0, 3450000, 'Paid', 'Hóa đơn phòng B202 tháng 03/2026 đã thanh toán, dùng cho dashboard doanh thu quá khứ.', TIMESTAMPTZ '2026-03-31 08:00:00Z', TIMESTAMPTZ '2026-04-02 09:00:00Z', NULL, NULL, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-B202-202603'), TIMESTAMPTZ '2026-03-31 08:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202604'), ended_contract_1_id, room_b202_id, guest_tenant_id, primary_landlord_id, 'HD-B202-202604', DATE '2026-04-01', DATE '2026-04-30', DATE '2026-04-30', DATE '2026-05-05', 3000000, 250000, 130000, 0, 3380000, 'Paid', 'Hóa đơn phòng B202 tháng 04/2026 đã thanh toán trước khi khách trả phòng.', TIMESTAMPTZ '2026-04-30 08:00:00Z', TIMESTAMPTZ '2026-05-02 09:00:00Z', NULL, NULL, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-B202-202604'), TIMESTAMPTZ '2026-04-30 08:00:00Z', now_utc),
                        (final_invoice_id, active_contract_id, room_b201_id, active_tenant_id, primary_landlord_id, 'DEMO-FLOW-INV-FINAL-DRAFT', DATE '2026-08-01', DATE '2026-08-15', NULL, DATE '2026-08-15', 0, 0, 80000, 0, 80000, 'Draft', 'Hóa đơn kỳ cuối để landlord phát hành sau khi tenant chấm dứt trước hạn; cọc bị chuyển về ví chủ trọ.', NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 10:00:00Z', now_utc)
                    ON CONFLICT (invoice_no) DO NOTHING;

                    INSERT INTO invoice_items (id, invoice_id, service_type_id, meter_reading_id, item_type, description, quantity, unit_price, amount, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JUNE-RENT'), june_paid_invoice_id, NULL, NULL, 'Rent', 'Tiền phòng tháng 05/2026 - hợp đồng 1 người', 1, 3600000, 3600000, TIMESTAMPTZ '2026-05-31 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JUNE-SERVICE'), june_paid_invoice_id, internet_service_id, NULL, 'Service', 'Internet + rác tháng 05/2026', 1, 160000, 160000, TIMESTAMPTZ '2026-05-31 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-RENT'), july_correct_invoice_id, NULL, NULL, 'Rent', 'Tiền phòng tháng 06/2026 - hợp đồng 1 người', 1, 3600000, 3600000, TIMESTAMPTZ '2026-06-30 09:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-ELECTRIC'), july_correct_invoice_id, electric_service_id, reading_electric_id, 'Service', 'Điện tháng 06/2026: 1341 - 1250 = 91 kWh', 91, 4000, 364000, TIMESTAMPTZ '2026-06-30 09:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-WATER'), july_correct_invoice_id, water_service_id, reading_water_id, 'Service', 'Nước tháng 06/2026: 96 - 88 = 8 m3', 8, 18000, 144000, TIMESTAMPTZ '2026-06-30 09:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-SERVICE'), july_correct_invoice_id, internet_service_id, NULL, 'Service', 'Internet + rác tháng 06/2026', 1, 120000, 120000, TIMESTAMPTZ '2026-06-30 09:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202602-RENT'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202602'), NULL, NULL, 'Rent', 'Tiền phòng B202 tháng 02/2026', 1, 3000000, 3000000, TIMESTAMPTZ '2026-02-28 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202602-SERVICE'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202602'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 02/2026', 1, 410000, 410000, TIMESTAMPTZ '2026-02-28 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202603-RENT'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202603'), NULL, NULL, 'Rent', 'Tiền phòng B202 tháng 03/2026', 1, 3000000, 3000000, TIMESTAMPTZ '2026-03-31 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202603-SERVICE'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202603'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 03/2026', 1, 450000, 450000, TIMESTAMPTZ '2026-03-31 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202604-RENT'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202604'), NULL, NULL, 'Rent', 'Tiền phòng B202 tháng 04/2026', 1, 3000000, 3000000, TIMESTAMPTZ '2026-04-30 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202604-SERVICE'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202604'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 04/2026', 1, 380000, 380000, TIMESTAMPTZ '2026-04-30 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-FINAL'), final_invoice_id, trash_service_id, NULL, 'Service', 'Phí vệ sinh kỳ cuối sau chấm dứt hợp đồng', 1, 80000, 80000, TIMESTAMPTZ '2026-07-15 10:00:00Z')
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO wallet_accounts (id, user_id, balance, reserved_balance, currency, status, created_at, updated_at)
                    VALUES
                        (search_wallet_id, search_tenant_id, 8000000, 0, 'VND', 'Active', seeded_at, now_utc),
                        (active_wallet_id, active_tenant_id, 50000000, 0, 'VND', 'Active', seeded_at, now_utc),
                        (landlord_wallet_id, primary_landlord_id, 12500000, 3600000, 'VND', 'Active', seeded_at, now_utc),
                        (secondary_landlord_wallet_id, secondary_landlord_id, 6200000, 0, 'VND', 'Active', seeded_at, now_utc)
                    ON CONFLICT (user_id) DO UPDATE SET
                        balance = EXCLUDED.balance,
                        reserved_balance = EXCLUDED.reserved_balance,
                        status = 'Active',
                        updated_at = now_utc;

                    INSERT INTO payment_transactions (id, wallet_account_id, payer_user_id, idempotency_key, amount, currency, payment_purpose, payment_method, provider_order_code, provider_transaction_code, provider_checkout_url, provider_qr_code, gateway_response_code, gateway_response_message, status, expires_at, paid_at, failed_at, confirmed_at, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-PAYMENT-ACTIVE-SEED-TOPUP'), active_wallet_id, active_tenant_id, 'demo-flow:active-tenant-seed-topup', 54280000, 'VND', 'WalletTopUp', 'Mock', 'DEMO-FLOW-TOPUP-ACTIVE-SEED', 'DEMO-FLOW-TOPUP-ACTIVE-SEED-TXN', 'https://pay.example/demo-flow/active-seed', NULL, '00', 'Nạp ví ban đầu cho Lê Quang Linh.', 'Succeeded', TIMESTAMPTZ '2026-06-01 10:00:00Z', TIMESTAMPTZ '2026-06-01 09:00:00Z', NULL, TIMESTAMPTZ '2026-06-01 09:00:00Z', TIMESTAMPTZ '2026-06-01 09:00:00Z', now_utc)
                    ON CONFLICT (idempotency_key) DO NOTHING;

                    INSERT INTO wallet_transactions (id, wallet_account_id, user_id, transfer_group_id, transaction_type, direction, amount, balance_before, balance_after, reserved_balance_before, reserved_balance_after, related_entity_type, related_entity_id, description, status, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-SEARCH-SEED'), search_wallet_id, search_tenant_id, NULL, 'WalletTopUp', 'Credit', 8000000, 0, 8000000, 0, 0, 'PaymentTransaction', NULL, 'DEMO-FLOW: nguyenxuanhuan.dev@gmail.com có 8.000.000 để demo thuê phòng mới.', 'Succeeded', seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-ACTIVE-SEED'), active_wallet_id, active_tenant_id, NULL, 'WalletTopUp', 'Credit', 54280000, 0, 54280000, 0, 0, 'PaymentTransaction', pg_temp.demo_flow_uuid('DEMO-FLOW-PAYMENT-ACTIVE-SEED-TOPUP'), 'Nạp ví ban đầu.', 'Succeeded', TIMESTAMPTZ '2026-06-01 09:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-LANDLORD-SEED'), landlord_wallet_id, primary_landlord_id, NULL, 'WalletTopUp', 'Credit', 12500000, 0, 12500000, 0, 3600000, 'PaymentTransaction', NULL, 'Nạp ví ban đầu cho chủ trọ.', 'Succeeded', seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-SECONDARY-LANDLORD-SEED'), secondary_landlord_wallet_id, secondary_landlord_id, NULL, 'WalletTopUp', 'Credit', 6200000, 0, 6200000, 0, 0, 'PaymentTransaction', NULL, 'DEMO-FLOW: landlord phụ có số dư dashboard.', 'Succeeded', seeded_at),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-JUNE-TENANT'), active_wallet_id, active_tenant_id, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-MAY'), 'InvoicePayment', 'Debit', 4280000, 54280000, 50000000, 0, 0, 'Invoice', june_paid_invoice_id, 'Thanh toán hóa đơn tháng 05/2026 phòng B201.', 'Succeeded', TIMESTAMPTZ '2026-06-01 09:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-JUNE-LANDLORD'), landlord_wallet_id, primary_landlord_id, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-MAY'), 'InvoiceReceive', 'Credit', 4280000, 8220000, 12500000, 3600000, 3600000, 'Invoice', june_paid_invoice_id, 'Nhận thanh toán hóa đơn tháng 05/2026 phòng B201.', 'Succeeded', TIMESTAMPTZ '2026-06-01 09:00:00Z')
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO withdrawal_requests (id, wallet_account_id, amount, fee, status, provider_order_code, provider_transaction_code, bank_bin, account_name, account_number, description, idempotency_key, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-WITHDRAW-PENDING'), landlord_wallet_id, 50000, 0, 'PendingApproval', 'DEMO-FLOW-WITHDRAW-50K', NULL, '970436', 'NGUYEN XUAN HUAN', '1234567890', 'Demo landlord rút 50.000 sau khi có tiền nhận hóa đơn.', 'demo-flow:withdraw-50k', TIMESTAMPTZ '2026-07-15 12:00:00Z', now_utc)
                    ON CONFLICT (idempotency_key) DO NOTHING;

                    INSERT INTO viewing_appointments (id, room_id, tenant_user_id, created_by_user_id, scheduled_at, duration_minutes, status, tenant_note, landlord_note, cancel_reason, responded_at, proposed_scheduled_at, proposed_duration_minutes, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-PENDING-1'), room_a101_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-16 09:00:00Z', 30, 'Pending', 'DEMO-FLOW: tenant phụ vừa đặt lịch xem A01 cho dashboard.', NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 08:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-PROPOSED-1'), room_a101_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-16 10:00:00Z', 30, 'Pending', 'DEMO-FLOW: tenant phụ có lịch đề xuất giờ mới cho dashboard.', 'Chủ trọ bận khung này, đề xuất 15:00 cùng ngày.', NULL, TIMESTAMPTZ '2026-07-15 08:30:00Z', TIMESTAMPTZ '2026-07-16 15:00:00Z', 30, TIMESTAMPTZ '2026-07-15 08:10:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-CONFIRMED-1'), room_a102_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-17 09:30:00Z', 30, 'Confirmed', 'DEMO-FLOW: lịch confirmed dashboard.', 'Đã xác nhận.', NULL, TIMESTAMPTZ '2026-07-15 09:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-14 08:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-CONFIRMED-2'), room_s101_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-18 14:00:00Z', 45, 'Confirmed', 'DEMO-FLOW: lịch confirmed Sunrise.', 'Đã xác nhận.', NULL, TIMESTAMPTZ '2026-07-15 09:30:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-14 09:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-COMPLETED-1'), room_b202_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-01 09:00:00Z', 30, 'Completed', 'DEMO-FLOW: lịch đã hoàn thành.', 'Khách đã xem phòng.', NULL, TIMESTAMPTZ '2026-07-01 09:40:00Z', NULL, NULL, TIMESTAMPTZ '2026-06-30 09:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-COMPLETED-2'), room_s101_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-02 10:00:00Z', 30, 'Completed', 'DEMO-FLOW: lịch đã hoàn thành Sunrise.', 'Khách đã xem phòng.', NULL, TIMESTAMPTZ '2026-07-02 10:40:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-01 09:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-REJECTED-1'), room_a102_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-03 11:00:00Z', 30, 'Rejected', 'DEMO-FLOW: lịch bị từ chối.', 'Không còn phù hợp khung giờ này.', NULL, TIMESTAMPTZ '2026-07-02 12:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-01 10:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-CANCEL-TENANT'), room_s101_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-04 13:00:00Z', 30, 'CancelledByTenant', 'DEMO-FLOW: tenant phụ hủy lịch.', NULL, 'Tenant bận việc cá nhân.', TIMESTAMPTZ '2026-07-03 12:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-02 10:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-APPT-CANCEL-LANDLORD'), room_a101_id, guest_tenant_id, guest_tenant_id, TIMESTAMPTZ '2026-07-05 16:00:00Z', 30, 'CancelledByLandlord', 'DEMO-FLOW: chủ trọ hủy lịch.', NULL, 'Chủ trọ có việc đột xuất.', TIMESTAMPTZ '2026-07-04 12:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-03 10:00:00Z', now_utc)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO conversations (id, type, title, room_id, rooming_house_id, direct_user_a_id, direct_user_b_id, created_by_user_id, last_message_at, last_message_preview, created_at, updated_at, is_closed, requires_join_approval, avatar_url)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-SEARCH-LANDLORD'), 'Direct', 'DEMO-FLOW: tenant phụ hỏi giảm giá phòng A01', room_a101_id, hoa_sen_house_id, LEAST(guest_tenant_id, primary_landlord_id), GREATEST(guest_tenant_id, primary_landlord_id), guest_tenant_id, TIMESTAMPTZ '2026-07-15 09:10:00Z', 'Dạ nếu thuê 12 tháng anh/chị hỗ trợ giảm còn 3.2 triệu được không?', TIMESTAMPTZ '2026-07-15 09:00:00Z', now_utc, FALSE, FALSE, NULL),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-INVOICE'), 'Direct', 'DEMO-FLOW: tenant hỏi hóa đơn bất hợp lý', room_b201_id, hoa_sen_house_id, LEAST(active_tenant_id, primary_landlord_id), GREATEST(active_tenant_id, primary_landlord_id), active_tenant_id, TIMESTAMPTZ '2026-07-15 09:20:00Z', 'Em thấy hóa đơn tháng này chưa khớp chỉ số, anh/chị kiểm tra giúp em.', TIMESTAMPTZ '2026-07-15 09:00:00Z', now_utc, FALSE, FALSE, NULL)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO conversation_participants (conversation_id, user_id, role, source, added_by_user_id, joined_at, left_at, last_read_at, unread_count, is_muted, inbox_status, inbox_status_updated_at, inbox_status_updated_by_user_id)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-SEARCH-LANDLORD'), guest_tenant_id, 'Member', 'Manual', guest_tenant_id, TIMESTAMPTZ '2026-07-15 09:00:00Z', NULL, TIMESTAMPTZ '2026-07-15 09:10:00Z', 0, FALSE, 'Main', TIMESTAMPTZ '2026-07-15 09:00:00Z', guest_tenant_id),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-SEARCH-LANDLORD'), primary_landlord_id, 'Member', 'Manual', guest_tenant_id, TIMESTAMPTZ '2026-07-15 09:00:00Z', NULL, NULL, 1, FALSE, 'Main', TIMESTAMPTZ '2026-07-15 09:00:00Z', guest_tenant_id),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-INVOICE'), active_tenant_id, 'Member', 'Manual', active_tenant_id, TIMESTAMPTZ '2026-07-15 09:00:00Z', NULL, TIMESTAMPTZ '2026-07-15 09:20:00Z', 0, FALSE, 'Main', TIMESTAMPTZ '2026-07-15 09:00:00Z', active_tenant_id),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-INVOICE'), primary_landlord_id, 'Member', 'Manual', active_tenant_id, TIMESTAMPTZ '2026-07-15 09:00:00Z', NULL, NULL, 1, FALSE, 'Main', TIMESTAMPTZ '2026-07-15 09:00:00Z', active_tenant_id)
                    ON CONFLICT DO NOTHING;

                    INSERT INTO chat_messages (id, conversation_id, sender_id, message_type, content, image_url, file_url, file_name, file_content_type, file_size, created_at, deleted_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-MSG-SEARCH-1'), pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-SEARCH-LANDLORD'), guest_tenant_id, 'Text', 'Dạ em xem phòng A01, nếu thuê 12 tháng anh/chị hỗ trợ giảm giá được không ạ?', NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 09:05:00Z', NULL),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-MSG-SEARCH-2'), pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-SEARCH-LANDLORD'), primary_landlord_id, 'Text', 'Nếu em thuê dài hạn và giữ phòng tốt, anh hỗ trợ chỉnh giá còn 3.200.000 cho kỳ đầu nhé.', NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 09:10:00Z', NULL),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-MSG-INVOICE-1'), pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-INVOICE'), active_tenant_id, 'Text', 'Em thấy hóa đơn tháng này chưa khớp chỉ số, anh/chị kiểm tra giúp em.', NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 09:15:00Z', NULL),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-MSG-INVOICE-2'), pg_temp.demo_flow_uuid('DEMO-FLOW-CONV-INVOICE'), primary_landlord_id, 'Text', 'Anh sẽ hủy hóa đơn cũ và tạo lại hóa đơn mới sau khi đọc lại chỉ số điện nước bằng AI.', NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 09:20:00Z', NULL)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO rooming_house_reviews (id, rooming_house_id, tenant_user_id, rental_contract_id, rating, comment, landlord_reply, landlord_reply_created_at, is_hidden, moderation_status, moderation_reason, ai_moderation_provider, ai_moderation_risk_level, ai_moderation_categories, ai_moderation_json, ai_reviewed_at, reviewed_by_admin_id, admin_reviewed_at, admin_note, created_at, updated_at)
                    VALUES
                        (review_reply_id, hoa_sen_house_id, guest_tenant_id, ended_contract_1_id, 5, 'Phòng sạch, khu vực yên tĩnh, chủ trọ hỗ trợ nhanh.', 'Cảm ơn bạn đã tin tưởng, mình sẽ tiếp tục giữ khu trọ sạch và an toàn.', TIMESTAMPTZ '2026-05-02 09:00:00Z', FALSE, 'Approved', NULL, 'Gemini', 'Low', '[]', '{"source":"demo-flow"}', TIMESTAMPTZ '2026-05-01 09:00:00Z', admin_user_id, TIMESTAMPTZ '2026-05-01 10:00:00Z', 'Review demo đã duyệt.', TIMESTAMPTZ '2026-05-01 08:00:00Z', now_utc),
                        (review_reported_id, sunrise_house_id, guest_tenant_id, ended_contract_2_id, 3, 'Wifi buổi tối hơi yếu, mong chủ trọ nâng cấp thêm.', 'Cảm ơn bạn đã góp ý, bên mình đã kiểm tra và nâng cấp modem khu vực tầng 2.', TIMESTAMPTZ '2026-05-03 09:00:00Z', FALSE, 'Approved', NULL, 'Gemini', 'Low', '[]', '{"source":"demo-flow"}', TIMESTAMPTZ '2026-05-02 09:00:00Z', admin_user_id, TIMESTAMPTZ '2026-05-02 10:00:00Z', 'Review demo có report đang chờ xử lý.', TIMESTAMPTZ '2026-05-02 08:00:00Z', now_utc)
                    ON CONFLICT (id) DO NOTHING;

                    INSERT INTO review_reports (id, rooming_house_review_id, reporter_user_id, reason, status, admin_note, created_at, resolved_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-REPORT-WIFI'), review_reported_id, active_tenant_id, 'Demo report review: nội dung cần admin kiểm tra trước khi giữ hiển thị.', 'Pending', NULL, TIMESTAMPTZ '2026-07-14 10:00:00Z', NULL)
                    ON CONFLICT (rooming_house_review_id, reporter_user_id) DO NOTHING;

                    UPDATE rooming_houses h
                    SET average_rating = COALESCE(r.avg_rating, 0),
                        total_reviews = COALESCE(r.total_reviews, 0),
                        updated_at = now_utc
                    FROM (
                        SELECT rooming_house_id, ROUND(AVG(rating)::numeric, 2) AS avg_rating, COUNT(*)::int AS total_reviews
                        FROM rooming_house_reviews
                        WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id)
                          AND is_hidden = FALSE
                          AND moderation_status IN ('Approved', 'PendingAdminReview')
                        GROUP BY rooming_house_id
                    ) r
                    WHERE h.id = r.rooming_house_id;
                END $demo$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                    hoa_sen_house_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-HOUSE-HOA-SEN');
                    sunrise_house_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-HOUSE-SUNRISE');
                    pending_house_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-HOUSE-PENDING');
                    room_a101_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-A101');
                    room_a102_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-A102');
                    room_b201_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-B201');
                    room_b202_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-B202');
                    room_s101_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-S101');
                    room_s102_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-S102');
                    room_p101_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-P101');
                    search_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-SEARCH-TENANT');
                    active_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-ACTIVE-TENANT');
                    landlord_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-PRIMARY-LANDLORD');
                    secondary_landlord_wallet_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-WALLET-SECONDARY-LANDLORD');
                BEGIN
                    DELETE FROM review_reports WHERE rooming_house_review_id IN (
                        SELECT id FROM rooming_house_reviews WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id)
                    );
                    DELETE FROM property_images WHERE object_key LIKE 'demo-flow/%';
                    DELETE FROM rooming_house_reviews WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM chat_messages WHERE conversation_id IN (SELECT id FROM conversations WHERE title LIKE 'DEMO-FLOW:%');
                    DELETE FROM conversation_participants WHERE conversation_id IN (SELECT id FROM conversations WHERE title LIKE 'DEMO-FLOW:%');
                    DELETE FROM conversations WHERE title LIKE 'DEMO-FLOW:%';
                    DELETE FROM invoice_items WHERE invoice_id IN (SELECT id FROM invoices WHERE invoice_no LIKE 'DEMO-FLOW-%');
                    DELETE FROM meter_readings
                    WHERE proof_image_object_key LIKE 'demo-flow/%'
                       OR id IN (
                            pg_temp.demo_flow_uuid('DEMO-FLOW-METER-ELECTRIC-202607'),
                            pg_temp.demo_flow_uuid('DEMO-FLOW-METER-WATER-202607')
                       );
                    DELETE FROM invoices WHERE invoice_no LIKE 'DEMO-FLOW-%';
                    DELETE FROM contract_signatures WHERE contract_signatures.contract_id IN (SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%') OR contract_signatures.appendix_id IN (SELECT id FROM contract_appendices WHERE appendix_number LIKE 'PL-DEMO-%');
                    DELETE FROM contract_files WHERE storage_object_key LIKE 'demo-flow/%';
                    DELETE FROM contract_appendix_changes WHERE contract_appendix_changes.appendix_id IN (SELECT id FROM contract_appendices WHERE appendix_number LIKE 'PL-DEMO-%');
                    DELETE FROM contract_appendices WHERE appendix_number LIKE 'PL-DEMO-%';
                    DELETE FROM contract_occupant_documents WHERE contract_occupant_id IN (SELECT id FROM contract_occupants WHERE contract_id IN (SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%'));
                    DELETE FROM contract_occupants WHERE contract_id IN (SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%');
                    DELETE FROM contracts WHERE contract_number LIKE 'DEMO-FLOW-%';
                    DELETE FROM room_deposits WHERE note LIKE 'DEMO-FLOW:%';
                    DELETE FROM rental_requests WHERE tenant_note LIKE 'DEMO-FLOW:%';
                    DELETE FROM viewing_appointments WHERE tenant_note LIKE 'DEMO-FLOW:%';
                    DELETE FROM favorite_rooming_houses WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM withdrawal_requests WHERE idempotency_key LIKE 'demo-flow:%';
                    DELETE FROM payment_transactions WHERE idempotency_key LIKE 'demo-flow:%';
                    DELETE FROM wallet_transactions WHERE description LIKE 'DEMO-FLOW:%'
                        OR wallet_account_id IN (search_wallet_id, active_wallet_id, landlord_wallet_id, secondary_landlord_wallet_id);
                    DELETE FROM wallet_accounts WHERE id IN (search_wallet_id, active_wallet_id, landlord_wallet_id, secondary_landlord_wallet_id);
                    DELETE FROM room_amenities WHERE room_id IN (room_a101_id, room_a102_id, room_b201_id, room_b202_id, room_s101_id, room_s102_id, room_p101_id);
                    DELETE FROM room_price_tiers WHERE room_id IN (room_a101_id, room_a102_id, room_b201_id, room_b202_id, room_s101_id, room_s102_id, room_p101_id);
                    DELETE FROM rooms WHERE id IN (room_a101_id, room_a102_id, room_b201_id, room_b202_id, room_s101_id, room_s102_id, room_p101_id);
                    DELETE FROM rooming_house_service_prices WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_house_rules WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rental_policies WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_house_amenities WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_house_legal_documents WHERE rooming_house_id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                    DELETE FROM rooming_houses WHERE id IN (hoa_sen_house_id, sunrise_house_id, pending_house_id);
                END $demo$;
                """);
        }

        private static string PasswordHash()
        {
            return new PasswordHasher<object>().HashPassword(new object(), DefaultPassword);
        }

        private static string Quote(string text)
        {
            return $"'{text.Replace("'", "''")}'";
        }
    }
}
