using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715180000_SeedSecondaryLandlordBulkBillingAndGroupChat")]
    public partial class SeedSecondaryLandlordBulkBillingAndGroupChat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pg_temp.demo_bulk_uuid(input text) RETURNS uuid AS $fn$
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
                    landlord_id uuid;
                    admin_id uuid;
                    house_id uuid := pg_temp.demo_bulk_uuid('DEMO-FLOW-HOUSE-SUNRISE');
                    electric_service_id uuid;
                    water_service_id uuid;
                    internet_service_id uuid;
                    trash_service_id uuid;
                    room_numbers text[] := ARRAY['S101','S102','S103','S104','S105'];
                    tenant_names text[] := ARRAY['Trần Minh Khoa','Phạm Ngọc Mai','Đỗ Gia Huy','Võ Thảo Vy','Bùi Quốc An'];
                    monthly_rents numeric[] := ARRAY[3300000, 3400000, 3500000, 3600000, 3700000];
                    room_id uuid;
                    tenant_id uuid;
                    request_id uuid;
                    deposit_id uuid;
                    contract_id uuid;
                    group_conversation_id uuid := pg_temp.demo_bulk_uuid('DEMO-BULK-CONV-SUNRISE-HOUSE');
                    invoice_id uuid;
                    electric_reading_id uuid;
                    water_reading_id uuid;
                    period_start date;
                    period_end date;
                    invoice_no_value text;
                    electric_prev numeric;
                    electric_current numeric;
                    water_prev numeric;
                    water_current numeric;
                    electric_consumption numeric;
                    water_consumption numeric;
                    rent_amount numeric;
                    utility_amount numeric;
                    service_amount numeric;
                    total_amount numeric;
                    month_index int;
                    i int;
                BEGIN
                    SELECT id INTO landlord_id
                    FROM users
                    WHERE normalized_email = 'XUNHUNS21@GMAIL.COM'
                    LIMIT 1;

                    SELECT id INTO admin_id
                    FROM users
                    WHERE normalized_email = 'ADMIN.DEMO@EXAMPLE.COM'
                    LIMIT 1;

                    SELECT id INTO electric_service_id FROM billing_service_types WHERE name = 'Điện' LIMIT 1;
                    SELECT id INTO water_service_id FROM billing_service_types WHERE name = 'Nước' LIMIT 1;
                    SELECT id INTO internet_service_id FROM billing_service_types WHERE name = 'Internet' LIMIT 1;
                    SELECT id INTO trash_service_id FROM billing_service_types WHERE name = 'Rác' LIMIT 1;

                    IF landlord_id IS NULL THEN
                        RAISE EXCEPTION 'Demo landlord xunhuns21@gmail.com is missing.';
                    END IF;

                    DELETE FROM chat_messages
                    WHERE chat_messages.conversation_id IN (SELECT id FROM conversations WHERE title LIKE 'DEMO-BULK:%');
                    DELETE FROM conversation_participants
                    WHERE conversation_participants.conversation_id IN (SELECT id FROM conversations WHERE title LIKE 'DEMO-BULK:%');
                    DELETE FROM conversations WHERE title LIKE 'DEMO-BULK:%';

                    DELETE FROM invoice_items
                    WHERE invoice_items.invoice_id IN (SELECT invoices.id FROM invoices WHERE invoices.invoice_no LIKE 'HD-SUNBULK-%');
                    DELETE FROM meter_readings
                    WHERE meter_readings.contract_id IN (SELECT contracts.id FROM contracts WHERE contracts.contract_number LIKE 'DEMO-BULK-SUNRISE-%');
                    DELETE FROM wallet_transactions
                    WHERE related_entity_type = 'Invoice'
                      AND wallet_transactions.related_entity_id IN (SELECT invoices.id FROM invoices WHERE invoices.invoice_no LIKE 'HD-SUNBULK-%');
                    DELETE FROM invoices WHERE invoices.invoice_no LIKE 'HD-SUNBULK-%';
                    DELETE FROM contract_occupants
                    WHERE contract_occupants.contract_id IN (SELECT contracts.id FROM contracts WHERE contracts.contract_number LIKE 'DEMO-BULK-SUNRISE-%');
                    DELETE FROM contracts WHERE contracts.contract_number LIKE 'DEMO-BULK-SUNRISE-%';
                    DELETE FROM room_deposits WHERE room_deposits.note LIKE 'DEMO-BULK:%';
                    DELETE FROM rental_requests WHERE rental_requests.tenant_note LIKE 'DEMO-BULK:%';

                    INSERT INTO rooming_houses (
                        id, landlord_user_id, name, description, address_line, ward_code, province_code,
                        address_display, latitude, longitude, google_map_url, approval_status, visibility_status,
                        average_rating, total_reviews, rejected_reason, reviewed_by_admin_id, reviewed_at,
                        created_at, updated_at, deleted_at
                    )
                    VALUES (
                        house_id, landlord_id, 'Nhà trọ Sunrise Demo',
                        'Khu trọ của chủ trọ phụ dùng riêng để demo tạo hóa đơn hàng loạt bằng AI và tạo nhóm chat theo khu trọ. Có 5 phòng đang thuê, mỗi phòng đã có hóa đơn các tháng trước.',
                        '88 Đường Số 7', '20194', '48',
                        '88 Đường Số 7, Phường Ngũ Hành Sơn, Thành phố Đà Nẵng',
                        15.9801000, 108.2619000, 'https://maps.example/demo-flow/sunrise-bulk-billing',
                        'Approved', 'Visible', 4.6, 4, NULL, admin_id, TIMESTAMPTZ '2026-07-15 09:00:00Z',
                        TIMESTAMPTZ '2026-07-15 00:00:00Z', now_utc, NULL
                    )
                    ON CONFLICT (id) DO UPDATE SET
                        landlord_user_id = EXCLUDED.landlord_user_id,
                        name = EXCLUDED.name,
                        description = EXCLUDED.description,
                        approval_status = EXCLUDED.approval_status,
                        visibility_status = EXCLUDED.visibility_status,
                        updated_at = now_utc,
                        deleted_at = NULL;

                    INSERT INTO rooming_house_service_prices (
                        id, rooming_house_id, service_type_id, pricing_unit, unit_price,
                        effective_from, effective_to, is_active, note, created_at, updated_at
                    )
                    VALUES
                        (pg_temp.demo_bulk_uuid('DEMO-FLOW-PRICE-SUN-ELECTRIC'), house_id, electric_service_id, 'MeterReading', 4200, DATE '2026-01-01', NULL, TRUE, 'Điện theo chỉ số cho demo AI tạo hóa đơn hàng loạt.', TIMESTAMPTZ '2026-07-15 00:00:00Z', now_utc),
                        (pg_temp.demo_bulk_uuid('DEMO-FLOW-PRICE-SUN-WATER'), house_id, water_service_id, 'MeterReading', 17000, DATE '2026-01-01', NULL, TRUE, 'Nước theo chỉ số cho demo AI tạo hóa đơn hàng loạt.', TIMESTAMPTZ '2026-07-15 00:00:00Z', now_utc),
                        (pg_temp.demo_bulk_uuid('DEMO-FLOW-PRICE-SUN-INTERNET'), house_id, internet_service_id, 'PerMonth', 100000, DATE '2026-01-01', NULL, TRUE, 'Internet cố định mỗi tháng.', TIMESTAMPTZ '2026-07-15 00:00:00Z', now_utc),
                        (pg_temp.demo_bulk_uuid('DEMO-FLOW-PRICE-SUN-WASTE'), house_id, trash_service_id, 'PerPersonPerMonth', 30000, DATE '2026-01-01', NULL, TRUE, 'Phí rác theo số người ở.', TIMESTAMPTZ '2026-07-15 00:00:00Z', now_utc)
                    ON CONFLICT (id) DO UPDATE SET
                        rooming_house_id = EXCLUDED.rooming_house_id,
                        service_type_id = EXCLUDED.service_type_id,
                        pricing_unit = EXCLUDED.pricing_unit,
                        unit_price = EXCLUDED.unit_price,
                        effective_to = NULL,
                        is_active = TRUE,
                        note = EXCLUDED.note,
                        updated_at = now_utc;

                    FOR i IN 1..array_length(room_numbers, 1) LOOP
                        room_id := pg_temp.demo_bulk_uuid('DEMO-BULK-ROOM-' || room_numbers[i]);
                        tenant_id := pg_temp.demo_bulk_uuid('DEMO-BULK-TENANT-' || i);
                        request_id := pg_temp.demo_bulk_uuid('DEMO-BULK-REQUEST-' || room_numbers[i]);
                        deposit_id := pg_temp.demo_bulk_uuid('DEMO-BULK-DEPOSIT-' || room_numbers[i]);
                        contract_id := pg_temp.demo_bulk_uuid('DEMO-BULK-CONTRACT-' || room_numbers[i]);

                        IF room_numbers[i] = 'S101' THEN
                            room_id := pg_temp.demo_bulk_uuid('DEMO-FLOW-ROOM-S101');
                        ELSIF room_numbers[i] = 'S102' THEN
                            room_id := pg_temp.demo_bulk_uuid('DEMO-FLOW-ROOM-S102');
                        END IF;

                        INSERT INTO users (
                            id, email, normalized_email, phone_number, password_hash, display_name,
                            avatar_url, status, onboarding_status, email_confirmed, phone_confirmed,
                            access_failed_count, lockout_end_at, last_login_at, created_at, updated_at, deleted_at
                        )
                        VALUES (
                            tenant_id,
                            'demo.bulk.tenant' || i || '@example.com',
                            upper('demo.bulk.tenant' || i || '@example.com'),
                            '09180000' || i,
                            NULL,
                            tenant_names[i],
                            NULL,
                            'Active',
                            'Completed',
                            TRUE,
                            TRUE,
                            0,
                            NULL,
                            NULL,
                            TIMESTAMPTZ '2026-02-20 08:00:00Z',
                            now_utc,
                            NULL
                        )
                        ON CONFLICT (normalized_email) DO UPDATE SET
                            display_name = EXCLUDED.display_name,
                            phone_number = EXCLUDED.phone_number,
                            status = 'Active',
                            onboarding_status = 'Completed',
                            email_confirmed = TRUE,
                            phone_confirmed = TRUE,
                            updated_at = now_utc,
                            deleted_at = NULL
                        RETURNING id INTO tenant_id;

                        INSERT INTO user_profiles (
                            user_id, full_name, date_of_birth, gender, address_line,
                            verified_citizen_id_masked, emergency_contact_name, emergency_contact_phone,
                            created_at, updated_at
                        )
                        VALUES (
                            tenant_id, tenant_names[i], DATE '1998-01-01' + (i * 80),
                            CASE WHEN i IN (2,4) THEN 'Female' ELSE 'Male' END,
                            'Đà Nẵng', '079********' || lpad((200 + i)::text, 3, '0'),
                            'Demo Bulk Contact', '09880000' || i,
                            TIMESTAMPTZ '2026-02-20 08:00:00Z', now_utc
                        )
                        ON CONFLICT (user_id) DO UPDATE SET
                            full_name = EXCLUDED.full_name,
                            updated_at = now_utc;

                        INSERT INTO user_roles (user_id, role_id, created_at)
                        VALUES (tenant_id, 2, TIMESTAMPTZ '2026-02-20 08:00:00Z')
                        ON CONFLICT DO NOTHING;

                        INSERT INTO rooms (
                            id, rooming_house_id, room_number, floor, area_m2, max_occupants,
                            is_tiered_pricing, status, description, created_at, updated_at, deleted_at
                        )
                        VALUES (
                            room_id, house_id, room_numbers[i], CASE WHEN i <= 2 THEN 1 ELSE 2 END,
                            22 + i, 2, TRUE, 'Occupied',
                            'DEMO-BULK: phòng đang thuê dùng để test tạo hóa đơn hàng loạt bằng AI.',
                            TIMESTAMPTZ '2026-02-20 08:00:00Z', now_utc, NULL
                        )
                        ON CONFLICT (id) DO UPDATE SET
                            rooming_house_id = EXCLUDED.rooming_house_id,
                            room_number = EXCLUDED.room_number,
                            floor = EXCLUDED.floor,
                            area_m2 = EXCLUDED.area_m2,
                            max_occupants = EXCLUDED.max_occupants,
                            is_tiered_pricing = TRUE,
                            status = 'Occupied',
                            description = EXCLUDED.description,
                            updated_at = now_utc,
                            deleted_at = NULL;

                        INSERT INTO room_price_tiers (
                            id, room_id, occupant_count, monthly_rent, is_active, created_at, updated_at
                        )
                        VALUES
                            (pg_temp.demo_bulk_uuid('DEMO-BULK-TIER-' || room_numbers[i] || '-1'), room_id, 1, monthly_rents[i], TRUE, TIMESTAMPTZ '2026-02-20 08:00:00Z', now_utc),
                            (pg_temp.demo_bulk_uuid('DEMO-BULK-TIER-' || room_numbers[i] || '-2'), room_id, 2, monthly_rents[i] + 350000, TRUE, TIMESTAMPTZ '2026-02-20 08:00:00Z', now_utc)
                        ON CONFLICT (id) DO UPDATE SET
                            monthly_rent = EXCLUDED.monthly_rent,
                            is_active = TRUE,
                            updated_at = now_utc;

                        INSERT INTO rental_requests (
                            id, room_id, tenant_user_id, approved_by_landlord_id, desired_start_date,
                            expected_end_date, expected_occupant_count, monthly_rent_snapshot,
                            deposit_amount_snapshot, tenant_note, status, responded_at, rejected_reason,
                            created_at, updated_at
                        )
                        VALUES (
                            request_id, room_id, tenant_id, landlord_id, DATE '2026-03-01',
                            DATE '2027-02-28', CASE WHEN i IN (2,5) THEN 2 ELSE 1 END,
                            monthly_rents[i], monthly_rents[i],
                            'DEMO-BULK: request accepted để tạo hợp đồng active cho bulk invoice và group chat.',
                            'Accepted', TIMESTAMPTZ '2026-02-25 09:00:00Z', NULL,
                            TIMESTAMPTZ '2026-02-24 09:00:00Z', now_utc
                        )
                        ON CONFLICT (id) DO UPDATE SET
                            room_id = EXCLUDED.room_id,
                            tenant_user_id = EXCLUDED.tenant_user_id,
                            approved_by_landlord_id = EXCLUDED.approved_by_landlord_id,
                            status = 'Accepted',
                            updated_at = now_utc;

                        INSERT INTO room_deposits (
                            id, rental_request_id, room_id, tenant_user_id, landlord_user_id,
                            deposit_amount, currency, status, payment_deadline_at, paid_at,
                            refunded_at, forfeited_at, refund_amount, forfeited_amount, note,
                            payment_transfer_group_id, refund_transfer_group_id, created_at, updated_at
                        )
                        VALUES (
                            deposit_id, request_id, room_id, tenant_id, landlord_id,
                            monthly_rents[i], 'VND', 'Paid',
                            TIMESTAMPTZ '2026-02-27 23:59:00Z',
                            TIMESTAMPTZ '2026-02-25 10:00:00Z',
                            NULL, NULL, NULL, NULL,
                            'DEMO-BULK: cọc đã thanh toán để hợp đồng active phục vụ billing/chat.',
                            pg_temp.demo_bulk_uuid('DEMO-BULK-TG-DEPOSIT-' || room_numbers[i]),
                            NULL,
                            TIMESTAMPTZ '2026-02-25 09:00:00Z',
                            now_utc
                        )
                        ON CONFLICT (id) DO UPDATE SET
                            status = 'Paid',
                            paid_at = EXCLUDED.paid_at,
                            updated_at = now_utc;

                        INSERT INTO contracts (
                            id, rental_request_id, room_deposit_id, room_id, main_tenant_user_id,
                            contract_number, start_date, end_date, monthly_rent, deposit_amount,
                            payment_day, status, room_snapshot, signature_deadline_at, activated_at,
                            termination_date, termination_type, status_reason, created_at, updated_at, deleted_at
                        )
                        VALUES (
                            contract_id, request_id, deposit_id, room_id, tenant_id,
                            'DEMO-BULK-SUNRISE-' || room_numbers[i] || '-20260301',
                            DATE '2026-03-01', DATE '2027-02-28',
                            monthly_rents[i], monthly_rents[i], 5, 'Active',
                            jsonb_build_object(
                                'RoomNumber', room_numbers[i],
                                'RoomingHouseName', 'Nhà trọ Sunrise Demo',
                                'MaxOccupants', 2,
                                'OccupantCount', CASE WHEN i IN (2,5) THEN 2 ELSE 1 END
                            ),
                            NULL, TIMESTAMPTZ '2026-02-25 15:00:00Z',
                            NULL, NULL, 'DEMO-BULK: hợp đồng active tối giản phục vụ tạo hóa đơn hàng loạt và quick contact chat.',
                            TIMESTAMPTZ '2026-02-25 09:00:00Z', now_utc, NULL
                        )
                        ON CONFLICT (contract_number) DO UPDATE SET
                            room_id = EXCLUDED.room_id,
                            main_tenant_user_id = EXCLUDED.main_tenant_user_id,
                            status = 'Active',
                            monthly_rent = EXCLUDED.monthly_rent,
                            room_snapshot = EXCLUDED.room_snapshot,
                            updated_at = now_utc,
                            deleted_at = NULL
                        RETURNING id INTO contract_id;

                        INSERT INTO contract_occupants (
                            id, contract_id, user_id, guardian_occupant_id, full_name, phone_number,
                            date_of_birth, relationship_to_main_tenant, move_in_date, move_out_date,
                            status, created_at, updated_at
                        )
                        VALUES (
                            pg_temp.demo_bulk_uuid('DEMO-BULK-OCC-' || room_numbers[i]),
                            contract_id, tenant_id, NULL, tenant_names[i], '09180000' || i,
                            DATE '1998-01-01' + (i * 80), 'Self',
                            DATE '2026-03-01', NULL, 'Active',
                            TIMESTAMPTZ '2026-02-25 15:00:00Z', now_utc
                        )
                        ON CONFLICT (id) DO UPDATE SET
                            contract_id = EXCLUDED.contract_id,
                            user_id = EXCLUDED.user_id,
                            full_name = EXCLUDED.full_name,
                            status = 'Active',
                            updated_at = now_utc;

                        electric_prev := 1000 + i * 100;
                        water_prev := 80 + i * 8;

                        FOR month_index IN 3..5 LOOP
                            period_start := make_date(2026, month_index, 1);
                            period_end := (period_start + INTERVAL '1 month - 1 day')::date;
                            invoice_no_value := 'HD-SUNBULK-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0');
                            invoice_id := pg_temp.demo_bulk_uuid('DEMO-BULK-INVOICE-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0'));
                            electric_reading_id := pg_temp.demo_bulk_uuid('DEMO-BULK-READING-ELECTRIC-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0'));
                            water_reading_id := pg_temp.demo_bulk_uuid('DEMO-BULK-READING-WATER-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0'));
                            electric_consumption := 62 + i * 4 + month_index;
                            water_consumption := 6 + (i % 3) + (month_index - 3);
                            electric_current := electric_prev + electric_consumption;
                            water_current := water_prev + water_consumption;
                            rent_amount := monthly_rents[i];
                            utility_amount := electric_consumption * 4200 + water_consumption * 17000;
                            service_amount := 100000 + (CASE WHEN i IN (2,5) THEN 2 ELSE 1 END) * 30000;
                            total_amount := rent_amount + utility_amount + service_amount;

                            INSERT INTO meter_readings (
                                id, room_id, contract_id, service_type_id, billing_period_start,
                                billing_period_end, previous_reading, current_reading, consumption,
                                proof_image_object_key, ai_reading, ai_raw_text, was_manually_corrected,
                                recorded_by_landlord_user_id, reading_at, created_at, updated_at
                            )
                            VALUES
                                (
                                    electric_reading_id, room_id, contract_id, electric_service_id,
                                    period_start, period_end, electric_prev, electric_current, electric_consumption,
                                    'demo-bulk/meters/' || lower(room_numbers[i]) || '-electric-2026' || lpad(month_index::text, 2, '0') || '.png',
                                    electric_current,
                                    'DEMO-BULK AI OCR: điện phòng ' || room_numbers[i] || ' = ' || electric_current || ' kWh',
                                    FALSE, landlord_id, period_end + TIME '08:00',
                                    period_end + TIME '08:00', now_utc
                                ),
                                (
                                    water_reading_id, room_id, contract_id, water_service_id,
                                    period_start, period_end, water_prev, water_current, water_consumption,
                                    'demo-bulk/meters/' || lower(room_numbers[i]) || '-water-2026' || lpad(month_index::text, 2, '0') || '.png',
                                    water_current,
                                    'DEMO-BULK AI OCR: nước phòng ' || room_numbers[i] || ' = ' || water_current || ' m3',
                                    FALSE, landlord_id, period_end + TIME '08:05',
                                    period_end + TIME '08:05', now_utc
                                )
                            ON CONFLICT (id) DO UPDATE SET
                                previous_reading = EXCLUDED.previous_reading,
                                current_reading = EXCLUDED.current_reading,
                                consumption = EXCLUDED.consumption,
                                proof_image_object_key = EXCLUDED.proof_image_object_key,
                                ai_reading = EXCLUDED.ai_reading,
                                ai_raw_text = EXCLUDED.ai_raw_text,
                                updated_at = now_utc;

                            INSERT INTO invoices (
                                id, contract_id, room_id, tenant_user_id, landlord_user_id, invoice_no,
                                billing_period_start, billing_period_end, issue_date, due_date,
                                rent_amount, utility_amount, service_amount, discount_amount, total_amount,
                                status, note, sent_at, paid_at, cancelled_at, cancel_reason,
                                created_at, updated_at, wallet_transfer_group_id
                            )
                            VALUES (
                                invoice_id, contract_id, room_id, tenant_id, landlord_id, invoice_no_value,
                                period_start, period_end, period_end, period_end + 5,
                                rent_amount, utility_amount, service_amount, 0, total_amount,
                                'Paid',
                                'DEMO-BULK: hóa đơn đã thanh toán tháng ' || lpad(month_index::text, 2, '0') || '/2026 cho dashboard và lịch sử thanh toán.',
                                period_end + TIME '09:00',
                                (period_end + 2) + TIME '09:00',
                                NULL, NULL,
                                period_end + TIME '09:00',
                                now_utc,
                                pg_temp.demo_bulk_uuid('DEMO-BULK-TG-INVOICE-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0'))
                            )
                            ON CONFLICT (invoice_no) DO UPDATE SET
                                contract_id = EXCLUDED.contract_id,
                                room_id = EXCLUDED.room_id,
                                tenant_user_id = EXCLUDED.tenant_user_id,
                                landlord_user_id = EXCLUDED.landlord_user_id,
                                billing_period_start = EXCLUDED.billing_period_start,
                                billing_period_end = EXCLUDED.billing_period_end,
                                issue_date = EXCLUDED.issue_date,
                                due_date = EXCLUDED.due_date,
                                rent_amount = EXCLUDED.rent_amount,
                                utility_amount = EXCLUDED.utility_amount,
                                service_amount = EXCLUDED.service_amount,
                                discount_amount = EXCLUDED.discount_amount,
                                total_amount = EXCLUDED.total_amount,
                                status = 'Paid',
                                note = EXCLUDED.note,
                                sent_at = EXCLUDED.sent_at,
                                paid_at = EXCLUDED.paid_at,
                                wallet_transfer_group_id = EXCLUDED.wallet_transfer_group_id,
                                updated_at = now_utc;

                            INSERT INTO invoice_items (
                                id, invoice_id, service_type_id, meter_reading_id, item_type,
                                description, quantity, unit_price, amount, created_at
                            )
                            VALUES
                                (pg_temp.demo_bulk_uuid('DEMO-BULK-ITEM-RENT-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0')), invoice_id, NULL, NULL, 'Rent', 'Tiền phòng ' || room_numbers[i] || ' tháng ' || lpad(month_index::text, 2, '0') || '/2026', 1, rent_amount, rent_amount, period_end + TIME '09:00'),
                                (pg_temp.demo_bulk_uuid('DEMO-BULK-ITEM-ELECTRIC-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0')), invoice_id, electric_service_id, electric_reading_id, 'Service', 'Điện (' || electric_consumption || ' kWh)', electric_consumption, 4200, electric_consumption * 4200, period_end + TIME '09:00'),
                                (pg_temp.demo_bulk_uuid('DEMO-BULK-ITEM-WATER-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0')), invoice_id, water_service_id, water_reading_id, 'Service', 'Nước (' || water_consumption || ' m3)', water_consumption, 17000, water_consumption * 17000, period_end + TIME '09:00'),
                                (pg_temp.demo_bulk_uuid('DEMO-BULK-ITEM-INTERNET-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0')), invoice_id, internet_service_id, NULL, 'Service', 'Internet tháng ' || lpad(month_index::text, 2, '0') || '/2026', 1, 100000, 100000, period_end + TIME '09:00'),
                                (pg_temp.demo_bulk_uuid('DEMO-BULK-ITEM-TRASH-' || room_numbers[i] || '-2026' || lpad(month_index::text, 2, '0')), invoice_id, trash_service_id, NULL, 'Service', 'Phí rác tháng ' || lpad(month_index::text, 2, '0') || '/2026', CASE WHEN i IN (2,5) THEN 2 ELSE 1 END, 30000, (CASE WHEN i IN (2,5) THEN 2 ELSE 1 END) * 30000, period_end + TIME '09:00')
                            ON CONFLICT (id) DO UPDATE SET
                                invoice_id = EXCLUDED.invoice_id,
                                service_type_id = EXCLUDED.service_type_id,
                                meter_reading_id = EXCLUDED.meter_reading_id,
                                item_type = EXCLUDED.item_type,
                                description = EXCLUDED.description,
                                quantity = EXCLUDED.quantity,
                                unit_price = EXCLUDED.unit_price,
                                amount = EXCLUDED.amount;

                            electric_prev := electric_current;
                            water_prev := water_current;
                        END LOOP;
                    END LOOP;

                    INSERT INTO conversations (
                        id, type, title, room_id, rooming_house_id, direct_user_a_id, direct_user_b_id,
                        created_by_user_id, last_message_at, last_message_preview, created_at, updated_at,
                        is_closed, requires_join_approval, avatar_url
                    )
                    VALUES (
                        group_conversation_id, 'Group', 'DEMO-BULK: Nhóm cư dân Sunrise - thông báo hóa đơn',
                        NULL, house_id, NULL, NULL, landlord_id,
                        TIMESTAMPTZ '2026-07-15 10:05:00Z',
                        'Chủ trọ sẽ tạo hóa đơn tháng 06 bằng AI, mọi người kiểm tra chỉ số giúp nhé.',
                        TIMESTAMPTZ '2026-07-15 10:00:00Z', now_utc,
                        FALSE, FALSE, NULL
                    )
                    ON CONFLICT (id) DO UPDATE SET
                        title = EXCLUDED.title,
                        rooming_house_id = EXCLUDED.rooming_house_id,
                        last_message_at = EXCLUDED.last_message_at,
                        last_message_preview = EXCLUDED.last_message_preview,
                        updated_at = now_utc,
                        is_closed = FALSE;

                    INSERT INTO conversation_participants (
                        conversation_id, user_id, role, source, added_by_user_id, joined_at,
                        left_at, last_read_at, unread_count, is_muted, inbox_status,
                        inbox_status_updated_at, inbox_status_updated_by_user_id
                    )
                    VALUES (
                        group_conversation_id, landlord_id, 'Owner', 'Manual', landlord_id,
                        TIMESTAMPTZ '2026-07-15 10:00:00Z', NULL,
                        TIMESTAMPTZ '2026-07-15 10:05:00Z', 0, FALSE, 'Main',
                        TIMESTAMPTZ '2026-07-15 10:00:00Z', landlord_id
                    )
                    ON CONFLICT (conversation_id, user_id) DO UPDATE SET
                        role = 'Owner',
                        source = 'Manual',
                        left_at = NULL,
                        inbox_status = 'Main',
                        unread_count = 0;

                    FOR i IN 1..array_length(room_numbers, 1) LOOP
                        tenant_id := pg_temp.demo_bulk_uuid('DEMO-BULK-TENANT-' || i);
                        SELECT id INTO tenant_id
                        FROM users
                        WHERE normalized_email = upper('demo.bulk.tenant' || i || '@example.com')
                        LIMIT 1;

                        INSERT INTO conversation_participants (
                            conversation_id, user_id, role, source, added_by_user_id, joined_at,
                            left_at, last_read_at, unread_count, is_muted, inbox_status,
                            inbox_status_updated_at, inbox_status_updated_by_user_id
                        )
                        VALUES (
                            group_conversation_id, tenant_id, 'Member', 'RoomQuickPick', landlord_id,
                            TIMESTAMPTZ '2026-07-15 10:00:00Z', NULL,
                            NULL, CASE WHEN i IN (1,3,5) THEN 1 ELSE 0 END, FALSE, 'Main',
                            TIMESTAMPTZ '2026-07-15 10:00:00Z', landlord_id
                        )
                        ON CONFLICT (conversation_id, user_id) DO UPDATE SET
                            role = 'Member',
                            source = 'RoomQuickPick',
                            left_at = NULL,
                            inbox_status = 'Main',
                            unread_count = EXCLUDED.unread_count;
                    END LOOP;

                    INSERT INTO chat_messages (
                        id, conversation_id, sender_id, message_type, content, image_url,
                        file_url, file_name, file_content_type, file_size, created_at, deleted_at
                    )
                    VALUES
                        (
                            pg_temp.demo_bulk_uuid('DEMO-BULK-MSG-SUNRISE-1'), group_conversation_id, landlord_id,
                            'Text', 'Chào mọi người, nhóm này dùng để thông báo hóa đơn và chỉ số điện nước của Nhà trọ Sunrise Demo.',
                            NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 10:01:00Z', NULL
                        ),
                        (
                            pg_temp.demo_bulk_uuid('DEMO-BULK-MSG-SUNRISE-2'), group_conversation_id, landlord_id,
                            'Text', 'Tháng này mình sẽ tạo hóa đơn hàng loạt bằng AI đọc ảnh công tơ, mọi người kiểm tra giúp nếu chỉ số lệch nhé.',
                            NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 10:05:00Z', NULL
                        )
                    ON CONFLICT (id) DO UPDATE SET
                        content = EXCLUDED.content,
                        created_at = EXCLUDED.created_at,
                        deleted_at = NULL;
                END $demo$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
