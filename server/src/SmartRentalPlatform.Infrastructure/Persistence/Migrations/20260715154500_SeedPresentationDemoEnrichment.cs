using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715154500_SeedPresentationDemoEnrichment")]
    public partial class SeedPresentationDemoEnrichment : Migration
    {
        private const string DefaultPassword = "Demo@123456";
        private static bool LegacyDemoSeedIsDisabled() => true;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (LegacyDemoSeedIsDisabled())
            {
                // Legacy demo seed SQL targets pre-media columns. Current demo data is seeded by DevelopmentDataSeed.
                return;
            }

            var passwordHash = Quote(PasswordHash());

            migrationBuilder.Sql($$"""
                CREATE OR REPLACE FUNCTION pg_temp.demo_enrich_uuid(input text) RETURNS uuid AS $fn$
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
                    admin_user_id uuid;
                    tenant_ids uuid[] := ARRAY[
                        pg_temp.demo_enrich_uuid('DEMO-ENRICH-TENANT-01'),
                        pg_temp.demo_enrich_uuid('DEMO-ENRICH-TENANT-02'),
                        pg_temp.demo_enrich_uuid('DEMO-ENRICH-TENANT-03'),
                        pg_temp.demo_enrich_uuid('DEMO-ENRICH-TENANT-04'),
                        pg_temp.demo_enrich_uuid('DEMO-ENRICH-TENANT-05'),
                        pg_temp.demo_enrich_uuid('DEMO-ENRICH-TENANT-06')
                    ];
                    tenant_names text[] := ARRAY[
                        'Minh Anh Demo',
                        'Gia Huy Demo',
                        'Thanh Lam Demo',
                        'Quốc Bảo Demo',
                        'Hà My Demo',
                        'Tuấn Khang Demo'
                    ];
                    tenant_id uuid;
                    house record;
                    idx int := 0;
                    rating int;
                    rent numeric(12,2);
                    request_id uuid;
                    deposit_id uuid;
                    contract_id uuid;
                    start_date_value date;
                    end_date_value date;
                BEGIN
                    SELECT u.id INTO admin_user_id
                    FROM users u
                    JOIN user_roles ur ON ur.user_id = u.id AND ur.role_id = 1
                    WHERE u.deleted_at IS NULL
                    ORDER BY u.created_at
                    LIMIT 1;

                    IF admin_user_id IS NULL THEN
                        admin_user_id := pg_temp.demo_enrich_uuid('DEMO-FLOW-USER-ADMIN');
                    END IF;

                    DELETE FROM review_reports WHERE reason LIKE 'DEMO-ENRICH:%';
                    DELETE FROM rooming_house_reviews WHERE comment LIKE 'DEMO-ENRICH:%';
                    DELETE FROM contract_occupant_documents
                    WHERE contract_occupant_id IN (
                        SELECT co.id
                        FROM contract_occupants co
                        JOIN contracts c ON c.id = co.contract_id
                        WHERE c.contract_number LIKE 'DEMO-ENRICH-%'
                    );
                    DELETE FROM contract_occupants
                    WHERE contract_occupants.contract_id IN (
                        SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-ENRICH-%'
                    );
                    DELETE FROM contracts WHERE contract_number LIKE 'DEMO-ENRICH-%';
                    DELETE FROM room_deposits WHERE note LIKE 'DEMO-ENRICH:%';
                    DELETE FROM rental_requests WHERE tenant_note LIKE 'DEMO-ENRICH:%';
                    DELETE FROM viewing_appointments WHERE tenant_note LIKE 'DEMO-ENRICH:%';

                    INSERT INTO users (id, email, normalized_email, phone_number, password_hash, display_name, avatar_url, status, onboarding_status, email_confirmed, phone_confirmed, access_failed_count, lockout_end_at, last_login_at, created_at, updated_at, deleted_at)
                    SELECT tenant_ids[i],
                           'demo.enrich.tenant' || i || '@example.com',
                           'DEMO.ENRICH.TENANT' || i || '@EXAMPLE.COM',
                           '09120000' || lpad(i::text, 2, '0'),
                           password_hash,
                           tenant_names[i],
                           NULL,
                           'Active',
                           'Completed',
                           TRUE,
                           FALSE,
                           0,
                           NULL,
                           seeded_at,
                           seeded_at,
                           now_utc,
                           NULL
                    FROM generate_subscripts(tenant_ids, 1) AS i
                    ON CONFLICT (normalized_email) DO UPDATE SET
                        password_hash = EXCLUDED.password_hash,
                        display_name = EXCLUDED.display_name,
                        status = 'Active',
                        onboarding_status = 'Completed',
                        email_confirmed = TRUE,
                        updated_at = now_utc,
                        deleted_at = NULL;

                    INSERT INTO user_profiles (user_id, full_name, date_of_birth, gender, address_line, verified_citizen_id_masked, emergency_contact_name, emergency_contact_phone, created_at, updated_at)
                    SELECT tenant_ids[i],
                           tenant_names[i],
                           DATE '1999-01-01' + (i * INTERVAL '120 days')::interval,
                           CASE WHEN i % 2 = 0 THEN 'Male' ELSE 'Female' END,
                           'Đà Nẵng',
                           '079********' || lpad((200 + i)::text, 3, '0'),
                           'Demo Support',
                           '09992000' || lpad(i::text, 2, '0'),
                           seeded_at,
                           now_utc
                    FROM generate_subscripts(tenant_ids, 1) AS i
                    ON CONFLICT (user_id) DO UPDATE SET
                        full_name = EXCLUDED.full_name,
                        address_line = EXCLUDED.address_line,
                        updated_at = now_utc;

                    INSERT INTO user_roles (user_id, role_id, created_at)
                    SELECT tenant_ids[i], 2, seeded_at
                    FROM generate_subscripts(tenant_ids, 1) AS i
                    ON CONFLICT DO NOTHING;

                    FOR house IN
                        SELECT h.id AS house_id,
                               h.name AS house_name,
                               h.landlord_user_id,
                               r.id AS room_id,
                               r.room_number,
                               COALESCE(r.max_occupants, 1) AS max_occupants,
                               COALESCE((
                                   SELECT MIN(rpt.monthly_rent)
                                   FROM room_price_tiers rpt
                                   WHERE rpt.room_id = r.id AND rpt.is_active = TRUE
                               ), 3000000)::numeric(12,2) AS monthly_rent
                        FROM rooming_houses h
                        JOIN LATERAL (
                            SELECT rr.*
                            FROM rooms rr
                            WHERE rr.rooming_house_id = h.id
                              AND rr.deleted_at IS NULL
                            ORDER BY CASE WHEN rr.status = 'Available' THEN 0 ELSE 1 END, rr.created_at
                            LIMIT 1
                        ) r ON TRUE
                        WHERE h.deleted_at IS NULL
                          AND h.approval_status = 'Approved'
                          AND h.visibility_status = 'Visible'
                        ORDER BY CASE WHEN h.name LIKE '%Demo%' THEN 0 ELSE 1 END, h.created_at DESC, h.name
                        LIMIT 30
                    LOOP
                        idx := idx + 1;
                        tenant_id := tenant_ids[((idx - 1) % array_length(tenant_ids, 1)) + 1];
                        rating := 3 + (idx % 3);
                        rent := GREATEST(house.monthly_rent, 1800000);
                        request_id := pg_temp.demo_enrich_uuid('DEMO-ENRICH-REQUEST-' || house.house_id::text);
                        deposit_id := pg_temp.demo_enrich_uuid('DEMO-ENRICH-DEPOSIT-' || house.house_id::text);
                        contract_id := pg_temp.demo_enrich_uuid('DEMO-ENRICH-CONTRACT-' || house.house_id::text);
                        start_date_value := (DATE '2025-01-01' + ((idx % 9) * INTERVAL '35 days'))::date;
                        end_date_value := (start_date_value + INTERVAL '89 days')::date;

                        INSERT INTO rental_requests (id, room_id, tenant_user_id, approved_by_landlord_id, desired_start_date, expected_end_date, expected_occupant_count, monthly_rent_snapshot, deposit_amount_snapshot, tenant_note, status, responded_at, rejected_reason, created_at, updated_at)
                        VALUES (request_id, house.room_id, tenant_id, house.landlord_user_id, start_date_value, end_date_value, LEAST(house.max_occupants, 2), rent, rent, 'DEMO-ENRICH: request quá khứ cho ' || house.house_name, 'Accepted', (start_date_value - INTERVAL '5 days'), NULL, (start_date_value - INTERVAL '7 days'), now_utc)
                        ON CONFLICT (id) DO NOTHING;

                        INSERT INTO room_deposits (id, rental_request_id, room_id, tenant_user_id, landlord_user_id, deposit_amount, currency, status, payment_deadline_at, paid_at, refunded_at, forfeited_at, refund_amount, forfeited_amount, note, payment_transfer_group_id, refund_transfer_group_id, created_at, updated_at)
                        VALUES (deposit_id, request_id, house.room_id, tenant_id, house.landlord_user_id, rent, 'VND', 'Refunded', (start_date_value - INTERVAL '3 days'), (start_date_value - INTERVAL '5 days'), end_date_value, NULL, rent, NULL, 'DEMO-ENRICH: cọc hợp đồng quá khứ cho ' || house.house_name, pg_temp.demo_enrich_uuid('DEMO-ENRICH-TG-DEPOSIT-' || house.house_id::text), pg_temp.demo_enrich_uuid('DEMO-ENRICH-TG-REFUND-' || house.house_id::text), (start_date_value - INTERVAL '7 days'), now_utc)
                        ON CONFLICT (id) DO NOTHING;

                        INSERT INTO contracts (id, rental_request_id, room_deposit_id, room_id, main_tenant_user_id, contract_number, start_date, end_date, monthly_rent, deposit_amount, payment_day, status, room_snapshot, signature_deadline_at, activated_at, termination_date, termination_type, status_reason, created_at, updated_at, deleted_at)
                        VALUES (contract_id, request_id, deposit_id, house.room_id, tenant_id, 'DEMO-ENRICH-' || substr(md5(house.house_id::text), 1, 12) || '-' || idx, start_date_value, end_date_value, rent, rent, 5 + (idx % 10), 'Expired', jsonb_build_object('RoomNumber', house.room_number, 'RoomingHouseName', house.house_name, 'MaxOccupants', house.max_occupants), NULL, (start_date_value - INTERVAL '4 days'), end_date_value, 'NormalExpiration', 'DEMO-ENRICH: hợp đồng quá khứ để demo review/dashboard.', (start_date_value - INTERVAL '7 days'), now_utc, NULL)
                        ON CONFLICT (contract_number) DO NOTHING;

                        INSERT INTO contract_occupants (id, contract_id, user_id, guardian_occupant_id, full_name, phone_number, date_of_birth, relationship_to_main_tenant, move_in_date, move_out_date, status, created_at, updated_at)
                        VALUES (pg_temp.demo_enrich_uuid('DEMO-ENRICH-OCC-' || house.house_id::text), contract_id, tenant_id, NULL, (SELECT display_name FROM users WHERE id = tenant_id), '0912999' || lpad(idx::text, 3, '0'), DATE '1999-01-01', 'Self', start_date_value, end_date_value, 'MoveOut', (start_date_value - INTERVAL '4 days'), now_utc)
                        ON CONFLICT (id) DO NOTHING;

                        INSERT INTO rooming_house_reviews (id, rooming_house_id, tenant_user_id, rental_contract_id, rating, comment, landlord_reply, landlord_reply_created_at, is_hidden, moderation_status, moderation_reason, ai_moderation_provider, ai_moderation_risk_level, ai_moderation_categories, ai_moderation_json, ai_reviewed_at, reviewed_by_admin_id, admin_reviewed_at, admin_note, created_at, updated_at)
                        VALUES (
                            pg_temp.demo_enrich_uuid('DEMO-ENRICH-REVIEW-' || house.house_id::text),
                            house.house_id,
                            tenant_id,
                            contract_id,
                            rating,
                            'DEMO-ENRICH: Khu trọ sạch, phản hồi nhanh, phù hợp để demo dữ liệu review nền.',
                            CASE
                                WHEN rating >= 5 THEN 'Cảm ơn bạn đã đánh giá tốt, chủ trọ sẽ tiếp tục giữ chất lượng dịch vụ.'
                                WHEN rating = 4 THEN 'Cảm ơn góp ý của bạn, bên mình đã bổ sung lịch vệ sinh và kiểm tra wifi định kỳ.'
                                ELSE 'Cảm ơn phản hồi, chủ trọ đã ghi nhận và cải thiện các điểm bạn góp ý.'
                            END,
                            end_date_value + INTERVAL '2 days',
                            FALSE,
                            'Approved',
                            NULL,
                            'Gemini',
                            'Low',
                            '[]',
                            '{"source":"demo-enrich"}',
                            end_date_value + INTERVAL '1 day',
                            admin_user_id,
                            end_date_value + INTERVAL '1 day 1 hour',
                            'DEMO-ENRICH: review nền đã duyệt.',
                            end_date_value + INTERVAL '1 day',
                            now_utc
                        )
                        ON CONFLICT (id) DO NOTHING;

                        INSERT INTO viewing_appointments (id, room_id, tenant_user_id, created_by_user_id, scheduled_at, duration_minutes, status, tenant_note, landlord_note, cancel_reason, responded_at, proposed_scheduled_at, proposed_duration_minutes, created_at, updated_at)
                        VALUES
                            (pg_temp.demo_enrich_uuid('DEMO-ENRICH-APPT-PENDING-' || house.house_id::text), house.room_id, tenant_ids[((idx) % array_length(tenant_ids, 1)) + 1], tenant_ids[((idx) % array_length(tenant_ids, 1)) + 1], TIMESTAMPTZ '2026-07-16 08:00:00Z' + (idx * INTERVAL '2 hours'), 30, 'Pending', 'DEMO-ENRICH: lịch chờ xác nhận cho dashboard.', NULL, NULL, NULL, NULL, NULL, TIMESTAMPTZ '2026-07-15 08:00:00Z', now_utc),
                            (pg_temp.demo_enrich_uuid('DEMO-ENRICH-APPT-CONFIRMED-' || house.house_id::text), house.room_id, tenant_ids[((idx + 1) % array_length(tenant_ids, 1)) + 1], tenant_ids[((idx + 1) % array_length(tenant_ids, 1)) + 1], TIMESTAMPTZ '2026-07-17 09:00:00Z' + (idx * INTERVAL '90 minutes'), 30, 'Confirmed', 'DEMO-ENRICH: lịch đã xác nhận cho dashboard.', 'Đã xác nhận, hẹn gặp bạn đúng giờ.', NULL, TIMESTAMPTZ '2026-07-15 09:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-15 08:30:00Z', now_utc),
                            (pg_temp.demo_enrich_uuid('DEMO-ENRICH-APPT-COMPLETED-' || house.house_id::text), house.room_id, tenant_id, tenant_id, TIMESTAMPTZ '2026-07-01 10:00:00Z' + (idx * INTERVAL '45 minutes'), 30, 'Completed', 'DEMO-ENRICH: lịch đã xem phòng.', 'Khách đã xem phòng và phản hồi tốt.', NULL, TIMESTAMPTZ '2026-07-01 11:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-06-30 10:00:00Z', now_utc),
                            (pg_temp.demo_enrich_uuid('DEMO-ENRICH-APPT-REJECTED-' || house.house_id::text), house.room_id, tenant_ids[((idx + 2) % array_length(tenant_ids, 1)) + 1], tenant_ids[((idx + 2) % array_length(tenant_ids, 1)) + 1], TIMESTAMPTZ '2026-07-03 14:00:00Z' + (idx * INTERVAL '30 minutes'), 30, 'Rejected', 'DEMO-ENRICH: lịch bị từ chối để dashboard có nhiều trạng thái.', 'Khung giờ này chủ trọ bận.', NULL, TIMESTAMPTZ '2026-07-02 14:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-01 14:00:00Z', now_utc),
                            (pg_temp.demo_enrich_uuid('DEMO-ENRICH-APPT-CANCEL-' || house.house_id::text), house.room_id, tenant_ids[((idx + 3) % array_length(tenant_ids, 1)) + 1], tenant_ids[((idx + 3) % array_length(tenant_ids, 1)) + 1], TIMESTAMPTZ '2026-07-04 15:00:00Z' + (idx * INTERVAL '25 minutes'), 30, 'CancelledByTenant', 'DEMO-ENRICH: tenant hủy lịch để demo dashboard.', NULL, 'Tenant đổi lịch cá nhân.', TIMESTAMPTZ '2026-07-03 15:00:00Z', NULL, NULL, TIMESTAMPTZ '2026-07-02 15:00:00Z', now_utc)
                        ON CONFLICT (id) DO NOTHING;
                    END LOOP;

                    UPDATE rooming_houses h
                    SET average_rating = COALESCE((
                            SELECT ROUND(AVG(r.rating)::numeric, 2)
                            FROM rooming_house_reviews r
                            WHERE r.rooming_house_id = h.id
                              AND r.is_hidden = FALSE
                              AND r.moderation_status IN ('Approved', 'PendingAdminReview')
                        ), 0),
                        total_reviews = COALESCE((
                            SELECT COUNT(*)::int
                            FROM rooming_house_reviews r
                            WHERE r.rooming_house_id = h.id
                              AND r.is_hidden = FALSE
                              AND r.moderation_status IN ('Approved', 'PendingAdminReview')
                        ), 0),
                        updated_at = now_utc
                    WHERE EXISTS (
                        SELECT 1
                        FROM rooming_house_reviews r
                        WHERE r.rooming_house_id = h.id
                          AND (r.comment LIKE 'DEMO-ENRICH:%' OR r.comment LIKE 'DEMO-FLOW:%')
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
                DELETE FROM review_reports WHERE reason LIKE 'DEMO-ENRICH:%';
                DELETE FROM rooming_house_reviews WHERE comment LIKE 'DEMO-ENRICH:%';
                DELETE FROM viewing_appointments WHERE tenant_note LIKE 'DEMO-ENRICH:%';
                DELETE FROM contract_occupant_documents
                WHERE contract_occupant_id IN (
                    SELECT co.id
                    FROM contract_occupants co
                    JOIN contracts c ON c.id = co.contract_id
                    WHERE c.contract_number LIKE 'DEMO-ENRICH-%'
                );
                DELETE FROM contract_occupants
                WHERE contract_occupants.contract_id IN (
                    SELECT id FROM contracts WHERE contract_number LIKE 'DEMO-ENRICH-%'
                );
                DELETE FROM contracts WHERE contract_number LIKE 'DEMO-ENRICH-%';
                DELETE FROM room_deposits WHERE note LIKE 'DEMO-ENRICH:%';
                DELETE FROM rental_requests WHERE tenant_note LIKE 'DEMO-ENRICH:%';

                UPDATE rooming_houses h
                SET average_rating = COALESCE((
                        SELECT ROUND(AVG(r.rating)::numeric, 2)
                        FROM rooming_house_reviews r
                        WHERE r.rooming_house_id = h.id
                          AND r.is_hidden = FALSE
                          AND r.moderation_status IN ('Approved', 'PendingAdminReview')
                    ), 0),
                    total_reviews = COALESCE((
                        SELECT COUNT(*)::int
                        FROM rooming_house_reviews r
                        WHERE r.rooming_house_id = h.id
                          AND r.is_hidden = FALSE
                          AND r.moderation_status IN ('Approved', 'PendingAdminReview')
                    ), 0),
                    updated_at = now()
                WHERE h.name LIKE '%Demo%' OR h.total_reviews > 0;
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
