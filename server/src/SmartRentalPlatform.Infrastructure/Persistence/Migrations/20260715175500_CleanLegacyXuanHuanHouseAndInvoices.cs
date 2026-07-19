using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715175500_CleanLegacyXuanHuanHouseAndInvoices")]
    public partial class CleanLegacyXuanHuanHouseAndInvoices : Migration
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
                CREATE OR REPLACE FUNCTION pg_temp.demo_seed_uuid(input text) RETURNS uuid AS $fn$
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
                    legacy_house_id uuid := '3b9c9ed2-2ed2-4ada-8cd3-3443e0518e2b';
                    legacy_room_a01_id uuid := '55123d51-6376-4c9b-8f0c-ccbf4f8a72d4';
                    legacy_room_a012_id uuid := 'ace196d3-b499-40a9-af15-efcc6ee941c1';
                    legacy_contract_id uuid;
                    legacy_tenant_id uuid;
                    legacy_landlord_id uuid;
                    internet_service_id uuid;
                    trash_service_id uuid;
                    final_invoice_id uuid;
                BEGIN
                    SELECT id INTO internet_service_id
                    FROM billing_service_types
                    WHERE lower(name) = lower('Internet')
                    LIMIT 1;

                    SELECT id INTO trash_service_id
                    FROM billing_service_types
                    WHERE lower(name) = lower('Rác')
                    LIMIT 1;

                    SELECT landlord_user_id INTO legacy_landlord_id
                    FROM rooming_houses
                    WHERE id = legacy_house_id;

                    SELECT id, main_tenant_user_id
                    INTO legacy_contract_id, legacy_tenant_id
                    FROM contracts
                    WHERE room_id = legacy_room_a01_id
                      AND contract_number LIKE 'DEMO-ENRICH-%'
                    ORDER BY created_at DESC
                    LIMIT 1;

                    UPDATE rooming_houses
                    SET name = 'Khu trọ An Bình Demo',
                        description = 'Khu trọ phụ dùng làm dữ liệu nền cho dashboard chủ trọ. Hai phòng A01 và A012 hiện đang trống; hợp đồng và yêu cầu thuê cũ chỉ nằm trong quá khứ.',
                        updated_at = now_utc
                    WHERE id = legacy_house_id;

                    UPDATE rooms
                    SET status = 'Available',
                        deleted_at = NULL,
                        updated_at = now_utc,
                        description = CASE room_number
                            WHEN 'A01' THEN 'Phòng A01 hiện đang trống sau hợp đồng quá khứ, có thể cho thuê lại.'
                            WHEN 'A012' THEN 'Phòng A012 hiện đang trống, chưa có hợp đồng active.'
                            ELSE description
                        END
                    WHERE id IN (legacy_room_a01_id, legacy_room_a012_id);

                    UPDATE rental_requests
                    SET tenant_note = replace(tenant_note, 'Khu trọ Xuân Huấn', 'Khu trọ An Bình Demo'),
                        updated_at = now_utc
                    WHERE room_id IN (legacy_room_a01_id, legacy_room_a012_id)
                      AND tenant_note LIKE 'DEMO-ENRICH:%';

                    UPDATE room_deposits
                    SET note = replace(note, 'Khu trọ Xuân Huấn', 'Khu trọ An Bình Demo'),
                        updated_at = now_utc
                    WHERE room_id IN (legacy_room_a01_id, legacy_room_a012_id)
                      AND note LIKE 'DEMO-ENRICH:%';

                    UPDATE contracts
                    SET room_snapshot = jsonb_set(
                            COALESCE(room_snapshot, '{}'::jsonb),
                            '{RoomingHouseName}',
                            '"Khu trọ An Bình Demo"'::jsonb,
                            TRUE
                        ),
                        updated_at = now_utc
                    WHERE room_id IN (legacy_room_a01_id, legacy_room_a012_id)
                      AND contract_number LIKE 'DEMO-ENRICH-%';

                    UPDATE rooming_house_reviews
                    SET comment = replace(comment, 'Khu trọ Xuân Huấn', 'Khu trọ An Bình Demo'),
                        updated_at = now_utc
                    WHERE rooming_house_id = legacy_house_id
                      AND comment LIKE 'DEMO-ENRICH:%';

                    SELECT id INTO final_invoice_id
                    FROM invoices
                    WHERE invoice_no = 'DEMO-FLOW-INV-FINAL-DRAFT'
                    LIMIT 1;

                    IF final_invoice_id IS NOT NULL THEN
                        DELETE FROM invoice_items
                        WHERE invoice_id = final_invoice_id;

                        DELETE FROM notifications
                        WHERE reference_id = final_invoice_id::text;

                        DELETE FROM wallet_transactions
                        WHERE related_entity_type = 'Invoice'
                          AND related_entity_id = final_invoice_id;

                        DELETE FROM invoices
                        WHERE id = final_invoice_id;
                    END IF;

                    IF legacy_contract_id IS NOT NULL
                       AND legacy_tenant_id IS NOT NULL
                       AND legacy_landlord_id IS NOT NULL THEN
                        INSERT INTO invoices (
                            id, contract_id, room_id, tenant_user_id, landlord_user_id, invoice_no,
                            billing_period_start, billing_period_end, issue_date, due_date,
                            rent_amount, utility_amount, service_amount, discount_amount, total_amount,
                            status, note, sent_at, paid_at, cancelled_at, cancel_reason,
                            created_at, updated_at, wallet_transfer_group_id
                        )
                        VALUES
                            (
                                pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202505'),
                                legacy_contract_id, legacy_room_a01_id, legacy_tenant_id, legacy_landlord_id,
                                'HD-ANBINH-A01-202505',
                                DATE '2025-05-01', DATE '2025-05-31', DATE '2025-05-31', DATE '2025-06-05',
                                2500000, 245000, 120000, 0, 2865000,
                                'Paid', 'Hóa đơn tháng 05/2025 của hợp đồng quá khứ phòng A01, đã thanh toán.',
                                TIMESTAMPTZ '2025-05-31 08:00:00Z', TIMESTAMPTZ '2025-06-02 09:00:00Z',
                                NULL, NULL, TIMESTAMPTZ '2025-05-31 08:00:00Z', now_utc,
                                pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-TG-A01-202505')
                            ),
                            (
                                pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202506'),
                                legacy_contract_id, legacy_room_a01_id, legacy_tenant_id, legacy_landlord_id,
                                'HD-ANBINH-A01-202506',
                                DATE '2025-06-01', DATE '2025-06-30', DATE '2025-06-30', DATE '2025-07-05',
                                2500000, 270000, 120000, 0, 2890000,
                                'Paid', 'Hóa đơn tháng 06/2025 của hợp đồng quá khứ phòng A01, đã thanh toán.',
                                TIMESTAMPTZ '2025-06-30 08:00:00Z', TIMESTAMPTZ '2025-07-02 09:00:00Z',
                                NULL, NULL, TIMESTAMPTZ '2025-06-30 08:00:00Z', now_utc,
                                pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-TG-A01-202506')
                            ),
                            (
                                pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202507'),
                                legacy_contract_id, legacy_room_a01_id, legacy_tenant_id, legacy_landlord_id,
                                'HD-ANBINH-A01-202507',
                                DATE '2025-07-01', DATE '2025-07-14', DATE '2025-07-14', DATE '2025-07-14',
                                1129000, 115000, 60000, 0, 1304000,
                                'Paid', 'Hóa đơn chốt kỳ đến ngày trả phòng 14/07/2025 của hợp đồng quá khứ phòng A01.',
                                TIMESTAMPTZ '2025-07-14 08:00:00Z', TIMESTAMPTZ '2025-07-14 10:00:00Z',
                                NULL, NULL, TIMESTAMPTZ '2025-07-14 08:00:00Z', now_utc,
                                pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-TG-A01-202507')
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
                            status = EXCLUDED.status,
                            note = EXCLUDED.note,
                            sent_at = EXCLUDED.sent_at,
                            paid_at = EXCLUDED.paid_at,
                            wallet_transfer_group_id = EXCLUDED.wallet_transfer_group_id,
                            updated_at = now_utc;

                        INSERT INTO invoice_items (id, invoice_id, service_type_id, meter_reading_id, item_type, description, quantity, unit_price, amount, created_at)
                        VALUES
                            (pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-ITEM-A01-202505-RENT'), pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202505'), NULL, NULL, 'Rent', 'Tiền phòng A01 tháng 05/2025', 1, 2500000, 2500000, TIMESTAMPTZ '2025-05-31 08:00:00Z'),
                            (pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-ITEM-A01-202505-SERVICE'), pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202505'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 05/2025', 1, 365000, 365000, TIMESTAMPTZ '2025-05-31 08:00:00Z'),
                            (pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-ITEM-A01-202506-RENT'), pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202506'), NULL, NULL, 'Rent', 'Tiền phòng A01 tháng 06/2025', 1, 2500000, 2500000, TIMESTAMPTZ '2025-06-30 08:00:00Z'),
                            (pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-ITEM-A01-202506-SERVICE'), pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202506'), internet_service_id, NULL, 'Service', 'Điện nước + internet/rác tháng 06/2025', 1, 390000, 390000, TIMESTAMPTZ '2025-06-30 08:00:00Z'),
                            (pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-ITEM-A01-202507-RENT'), pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202507'), NULL, NULL, 'Rent', 'Tiền phòng A01 từ 01/07 đến 14/07/2025', 1, 1129000, 1129000, TIMESTAMPTZ '2025-07-14 08:00:00Z'),
                            (pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-ITEM-A01-202507-SERVICE'), pg_temp.demo_seed_uuid('LEGACY-XUANHUAN-INVOICE-A01-202507'), trash_service_id, NULL, 'Service', 'Điện nước + internet/rác chốt kỳ 14/07/2025', 1, 175000, 175000, TIMESTAMPTZ '2025-07-14 08:00:00Z')
                        ON CONFLICT (id) DO UPDATE SET
                            invoice_id = EXCLUDED.invoice_id,
                            service_type_id = EXCLUDED.service_type_id,
                            item_type = EXCLUDED.item_type,
                            description = EXCLUDED.description,
                            quantity = EXCLUDED.quantity,
                            unit_price = EXCLUDED.unit_price,
                            amount = EXCLUDED.amount;
                    END IF;
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
}
    }
}
