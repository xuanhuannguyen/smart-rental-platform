using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715174500_AdjustXuanHuanDemoHouse")]
    public partial class AdjustXuanHuanDemoHouse : Migration
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
                    now_utc timestamptz := now();
                    primary_landlord_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-PRIMARY-LANDLORD');
                    active_tenant_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-ACTIVE-TENANT');
                    guest_tenant_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-USER-GUEST-TENANT');
                    house_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-HOUSE-HOA-SEN');
                    room_a01_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-A101');
                    room_a02_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-A102');
                    room_b201_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-B201');
                    room_b202_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-ROOM-B202');
                    active_contract_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-CONTRACT-ACTIVE');
                    ended_contract_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-CONTRACT-ENDED-1');
                    ended_request_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-REQUEST-ENDED-1');
                    ended_deposit_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-DEPOSIT-ENDED-1');
                    internet_service_id uuid;
                BEGIN
                    SELECT id INTO internet_service_id
                    FROM billing_service_types
                    WHERE lower(name) = lower('Internet')
                    LIMIT 1;

                    SELECT id INTO primary_landlord_id
                    FROM users
                    WHERE normalized_email = 'NGUYENXUANHUAN21102005@GMAIL.COM'
                    LIMIT 1;

                    SELECT id INTO active_tenant_id
                    FROM users
                    WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM'
                    LIMIT 1;

                    SELECT id INTO guest_tenant_id
                    FROM users
                    WHERE normalized_email = 'DEMO.FLOW.GUEST@EXAMPLE.COM'
                    LIMIT 1;

                    UPDATE users
                    SET display_name = 'Nguyễn Xuân Huân - Chủ trọ Xuân Huân',
                        updated_at = now_utc
                    WHERE id = primary_landlord_id;

                    UPDATE user_profiles
                    SET full_name = 'Nguyễn Xuân Huân - Chủ trọ Xuân Huân',
                        updated_at = now_utc
                    WHERE user_id = primary_landlord_id;

                    UPDATE kyc_verifications
                    SET ocr_full_name = 'Nguyễn Xuân Huân - Chủ trọ Xuân Huân',
                        updated_at = now_utc
                    WHERE user_id = primary_landlord_id;

                    UPDATE rooming_houses
                    SET name = 'Khu trọ Xuân Huân',
                        description = 'Khu trọ chính cho demo: phòng A01 còn trống để tenant đầu tiên thuê, phòng B201 đang có hợp đồng active của Lê Quang Linh, các phòng còn lại có lịch sử thuê và hóa đơn doanh thu các tháng trước.',
                        address_line = '144 Trần Đại Nghĩa',
                        address_display = '144 Trần Đại Nghĩa, Phường Ngũ Hành Sơn, Thành phố Đà Nẵng',
                        google_map_url = 'https://maps.example/demo-flow/xuan-huan',
                        approval_status = 'Approved',
                        visibility_status = 'Visible',
                        updated_at = now_utc,
                        deleted_at = NULL
                    WHERE id = house_id;

                    UPDATE rooms
                    SET room_number = 'A01',
                        max_occupants = 2,
                        is_tiered_pricing = TRUE,
                        status = 'Available',
                        description = 'Phòng A01 còn trống duy nhất cho account tenant đầu tiên tìm kiếm, đặt lịch, thuê và ký hợp đồng mới; giá thay đổi theo 1 hoặc 2 người ở.',
                        updated_at = now_utc,
                        deleted_at = NULL
                    WHERE id = room_a01_id;

                    UPDATE rooms
                    SET room_number = 'A02',
                        status = 'Maintenance',
                        description = 'Phòng A02 đã có lịch sử thuê đủ kỳ hóa đơn trước demo, hiện tạm bảo trì nên không xuất hiện trong luồng thuê mới.',
                        updated_at = now_utc,
                        deleted_at = NULL
                    WHERE id = room_a02_id;

                    UPDATE rooms
                    SET status = 'Occupied',
                        max_occupants = 2,
                        is_tiered_pricing = TRUE,
                        description = 'Phòng B201 cho tối đa 2 người; hợp đồng demo hiện tại của Lê Quang Linh chỉ có 1 người ở.',
                        updated_at = now_utc,
                        deleted_at = NULL
                    WHERE id = room_b201_id;

                    UPDATE rooms
                    SET status = 'Maintenance',
                        description = 'Phòng B202 có hợp đồng cũ và hóa đơn doanh thu 3 tháng trước, hiện đã trả phòng.',
                        updated_at = now_utc,
                        deleted_at = NULL
                    WHERE id = room_b202_id;

                    UPDATE room_price_tiers
                    SET monthly_rent = CASE occupant_count WHEN 1 THEN 3500000 WHEN 2 THEN 3900000 ELSE monthly_rent END,
                        is_active = TRUE,
                        updated_at = now_utc
                    WHERE room_id = room_a01_id;

                    UPDATE room_price_tiers
                    SET monthly_rent = CASE occupant_count WHEN 1 THEN 3600000 WHEN 2 THEN 3950000 ELSE monthly_rent END,
                        is_active = TRUE,
                        updated_at = now_utc
                    WHERE room_id = room_b201_id;

                    UPDATE contracts
                    SET room_snapshot = jsonb_set(COALESCE(room_snapshot, '{}'::jsonb), '{RoomingHouseName}', '"Khu trọ Xuân Huân"'::jsonb, TRUE),
                        updated_at = now_utc
                    WHERE id IN (active_contract_id, ended_contract_id);

                    UPDATE property_images
                    SET caption = 'Mặt tiền Khu trọ Xuân Huân'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-HOA-1');

                    UPDATE property_images
                    SET caption = 'Phòng A01'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-IMG-A101-1');

                    UPDATE conversations
                    SET title = replace(title, 'A101', 'A01'),
                        updated_at = now_utc
                    WHERE title LIKE 'DEMO-FLOW:%A101%';

                    UPDATE chat_messages
                    SET content = replace(content, 'A101', 'A01')
                    WHERE content LIKE '%A101%';

                    UPDATE viewing_appointments
                    SET tenant_note = replace(tenant_note, 'A101', 'A01'),
                        updated_at = now_utc
                    WHERE tenant_note LIKE '%A101%';

                    UPDATE rental_requests
                    SET room_id = room_a02_id,
                        updated_at = now_utc
                    WHERE room_id = room_a01_id
                      AND tenant_note LIKE 'DEMO-ENRICH:%';

                    UPDATE room_deposits
                    SET room_id = room_a02_id,
                        updated_at = now_utc
                    WHERE room_id = room_a01_id
                      AND note LIKE 'DEMO-ENRICH:%';

                    UPDATE contracts
                    SET room_id = room_a02_id,
                        room_snapshot = jsonb_set(
                            jsonb_set(COALESCE(room_snapshot, '{}'::jsonb), '{RoomNumber}', '"A02"'::jsonb, TRUE),
                            '{RoomingHouseName}',
                            '"Khu trọ Xuân Huân"'::jsonb,
                            TRUE
                        ),
                        updated_at = now_utc
                    WHERE room_id = room_a01_id
                      AND contract_number LIKE 'DEMO-ENRICH-%';

                    UPDATE viewing_appointments
                    SET room_id = room_a02_id,
                        tenant_note = replace(tenant_note, 'A01', 'A02'),
                        updated_at = now_utc
                    WHERE room_id = room_a01_id
                      AND tenant_note LIKE 'DEMO-ENRICH:%';

                    UPDATE contract_files
                    SET sha256_hash = CASE purpose
                            WHEN 'SignedLegalDocument' THEN 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce'
                            WHEN 'Preview' THEN '58bb3ebd5d963a985b2c5e0522bdc2d4d4ed450552b976c03ebe34821382b8ab'
                            ELSE sha256_hash
                        END
                    WHERE contract_id = active_contract_id
                      AND appendix_id IS NULL
                      AND storage_object_key IN ('demo-flow/contracts/active-b201-signed-vnpt.pdf', 'demo-flow/contracts/active-b201-preview-vnpt.pdf');

                    UPDATE contract_signatures
                    SET signed_file_sha256_hash = 'c8534ac9502e77051b80f02f5937762231ad63e6ae4473bd8c33c597f69c5fce',
                        provider_evidence_json = jsonb_set(
                            COALESCE(provider_evidence_json::jsonb, '{}'::jsonb),
                            '{document_number}',
                            '"HD-202604250900-B201-XUANHUAN"'::jsonb,
                            TRUE
                        )
                    WHERE contract_id = active_contract_id;

                    INSERT INTO rental_requests (id, room_id, tenant_user_id, approved_by_landlord_id, desired_start_date, expected_end_date, expected_occupant_count, monthly_rent_snapshot, deposit_amount_snapshot, tenant_note, status, responded_at, rejected_reason, created_at, updated_at)
                    VALUES (ended_request_id, room_b202_id, guest_tenant_id, primary_landlord_id, DATE '2026-02-01', DATE '2026-04-30', 1, 3000000, 3000000, 'DEMO-FLOW: request cũ phòng B202 để có hợp đồng và hóa đơn doanh thu 3 tháng trước.', 'Accepted', TIMESTAMPTZ '2026-01-25 09:00:00Z', NULL, TIMESTAMPTZ '2026-01-24 09:00:00Z', now_utc)
                    ON CONFLICT (id) DO UPDATE SET
                        room_id = EXCLUDED.room_id,
                        tenant_user_id = EXCLUDED.tenant_user_id,
                        approved_by_landlord_id = EXCLUDED.approved_by_landlord_id,
                        status = EXCLUDED.status,
                        updated_at = now_utc;

                    INSERT INTO room_deposits (id, rental_request_id, room_id, tenant_user_id, landlord_user_id, deposit_amount, currency, status, payment_deadline_at, paid_at, refunded_at, forfeited_at, refund_amount, forfeited_amount, note, payment_transfer_group_id, refund_transfer_group_id, created_at, updated_at)
                    VALUES (ended_deposit_id, ended_request_id, room_b202_id, guest_tenant_id, primary_landlord_id, 3000000, 'VND', 'Refunded', TIMESTAMPTZ '2026-01-28 23:59:00Z', TIMESTAMPTZ '2026-01-25 10:00:00Z', TIMESTAMPTZ '2026-04-30 10:00:00Z', NULL, 3000000, NULL, 'DEMO-FLOW: cọc hợp đồng cũ B202 đã hoàn sau khi trả phòng.', pg_temp.demo_flow_uuid('DEMO-FLOW-TG-DEPOSIT-ENDED-1'), pg_temp.demo_flow_uuid('DEMO-FLOW-TG-REFUND-ENDED-1'), TIMESTAMPTZ '2026-01-25 09:00:00Z', now_utc)
                    ON CONFLICT (id) DO UPDATE SET
                        status = EXCLUDED.status,
                        refunded_at = EXCLUDED.refunded_at,
                        refund_amount = EXCLUDED.refund_amount,
                        updated_at = now_utc;

                    INSERT INTO contracts (id, rental_request_id, room_deposit_id, room_id, main_tenant_user_id, contract_number, start_date, end_date, monthly_rent, deposit_amount, payment_day, status, room_snapshot, signature_deadline_at, activated_at, termination_date, termination_type, status_reason, created_at, updated_at, deleted_at)
                    VALUES (ended_contract_id, ended_request_id, ended_deposit_id, room_b202_id, guest_tenant_id, 'DEMO-FLOW-ENDED-B202-20260201', DATE '2026-02-01', DATE '2026-04-30', 3000000, 3000000, 5, 'Expired', '{"RoomNumber":"B202","RoomingHouseName":"Khu trọ Xuân Huân","MaxOccupants":1}'::jsonb, NULL, TIMESTAMPTZ '2026-01-25 15:00:00Z', DATE '2026-04-30', 'NormalExpiration', 'Hợp đồng cũ phòng B202 dùng để tạo doanh thu 3 tháng trước.', TIMESTAMPTZ '2026-01-25 09:00:00Z', now_utc, NULL)
                    ON CONFLICT (contract_number) DO UPDATE SET
                        status = EXCLUDED.status,
                        room_id = EXCLUDED.room_id,
                        main_tenant_user_id = EXCLUDED.main_tenant_user_id,
                        room_snapshot = EXCLUDED.room_snapshot,
                        updated_at = now_utc;

                    INSERT INTO contract_occupants (id, contract_id, user_id, guardian_occupant_id, full_name, phone_number, date_of_birth, relationship_to_main_tenant, move_in_date, move_out_date, status, created_at, updated_at)
                    VALUES (pg_temp.demo_flow_uuid('DEMO-FLOW-OCC-ENDED-1'), ended_contract_id, guest_tenant_id, NULL, 'Khách Thuê Demo Phụ', '0901000005', DATE '1999-05-05', 'Self', DATE '2026-02-01', DATE '2026-04-30', 'MoveOut', TIMESTAMPTZ '2026-01-25 15:00:00Z', now_utc)
                    ON CONFLICT (id) DO UPDATE SET
                        move_out_date = EXCLUDED.move_out_date,
                        status = EXCLUDED.status,
                        updated_at = now_utc;

                    INSERT INTO invoices (id, contract_id, room_id, tenant_user_id, landlord_user_id, invoice_no, billing_period_start, billing_period_end, issue_date, due_date, rent_amount, utility_amount, service_amount, discount_amount, total_amount, status, note, sent_at, paid_at, cancelled_at, cancel_reason, wallet_transfer_group_id, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202602'), ended_contract_id, room_b202_id, guest_tenant_id, primary_landlord_id, 'HD-B202-202602', DATE '2026-02-01', DATE '2026-02-28', DATE '2026-02-28', DATE '2026-03-05', 3000000, 280000, 130000, 0, 3410000, 'Paid', 'Hóa đơn phòng B202 tháng 02/2026 đã thanh toán, dùng cho dashboard doanh thu quá khứ.', TIMESTAMPTZ '2026-02-28 08:00:00Z', TIMESTAMPTZ '2026-03-02 09:00:00Z', NULL, NULL, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-B202-202602'), TIMESTAMPTZ '2026-02-28 08:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202603'), ended_contract_id, room_b202_id, guest_tenant_id, primary_landlord_id, 'HD-B202-202603', DATE '2026-03-01', DATE '2026-03-31', DATE '2026-03-31', DATE '2026-04-05', 3000000, 320000, 130000, 0, 3450000, 'Paid', 'Hóa đơn phòng B202 tháng 03/2026 đã thanh toán, dùng cho dashboard doanh thu quá khứ.', TIMESTAMPTZ '2026-03-31 08:00:00Z', TIMESTAMPTZ '2026-04-02 09:00:00Z', NULL, NULL, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-B202-202603'), TIMESTAMPTZ '2026-03-31 08:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202604'), ended_contract_id, room_b202_id, guest_tenant_id, primary_landlord_id, 'HD-B202-202604', DATE '2026-04-01', DATE '2026-04-30', DATE '2026-04-30', DATE '2026-05-05', 3000000, 250000, 130000, 0, 3380000, 'Paid', 'Hóa đơn phòng B202 tháng 04/2026 đã thanh toán trước khi khách trả phòng.', TIMESTAMPTZ '2026-04-30 08:00:00Z', TIMESTAMPTZ '2026-05-02 09:00:00Z', NULL, NULL, pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-B202-202604'), TIMESTAMPTZ '2026-04-30 08:00:00Z', now_utc)
                    ON CONFLICT (invoice_no) DO UPDATE SET
                        status = EXCLUDED.status,
                        rent_amount = EXCLUDED.rent_amount,
                        utility_amount = EXCLUDED.utility_amount,
                        service_amount = EXCLUDED.service_amount,
                        total_amount = EXCLUDED.total_amount,
                        paid_at = EXCLUDED.paid_at,
                        updated_at = now_utc;

                    INSERT INTO invoice_items (id, invoice_id, service_type_id, meter_reading_id, item_type, description, quantity, unit_price, amount, created_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202602-RENT'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202602'), NULL, NULL, 'Rent', 'Tiền phòng B202 tháng 02/2026', 1, 3000000, 3000000, TIMESTAMPTZ '2026-02-28 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202602-SERVICE'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202602'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 02/2026', 1, 410000, 410000, TIMESTAMPTZ '2026-02-28 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202603-RENT'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202603'), NULL, NULL, 'Rent', 'Tiền phòng B202 tháng 03/2026', 1, 3000000, 3000000, TIMESTAMPTZ '2026-03-31 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202603-SERVICE'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202603'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 03/2026', 1, 450000, 450000, TIMESTAMPTZ '2026-03-31 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202604-RENT'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202604'), NULL, NULL, 'Rent', 'Tiền phòng B202 tháng 04/2026', 1, 3000000, 3000000, TIMESTAMPTZ '2026-04-30 08:00:00Z'),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-B202-202604-SERVICE'), pg_temp.demo_flow_uuid('DEMO-FLOW-INVOICE-B202-202604'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 04/2026', 1, 380000, 380000, TIMESTAMPTZ '2026-04-30 08:00:00Z')
                    ON CONFLICT (id) DO UPDATE SET
                        description = EXCLUDED.description,
                        quantity = EXCLUDED.quantity,
                        unit_price = EXCLUDED.unit_price,
                        amount = EXCLUDED.amount;
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
                UPDATE rooming_houses
                SET name = 'Nhà trọ Hoa Sen Demo'
                WHERE id = (
                    SELECT (
                        substr(md5('DEMO-FLOW-HOUSE-HOA-SEN'), 1, 8) || '-' ||
                        substr(md5('DEMO-FLOW-HOUSE-HOA-SEN'), 9, 4) || '-' ||
                        substr(md5('DEMO-FLOW-HOUSE-HOA-SEN'), 13, 4) || '-' ||
                        substr(md5('DEMO-FLOW-HOUSE-HOA-SEN'), 17, 4) || '-' ||
                        substr(md5('DEMO-FLOW-HOUSE-HOA-SEN'), 21, 12)
                    )::uuid
                );
                """);
        }
    }
}
