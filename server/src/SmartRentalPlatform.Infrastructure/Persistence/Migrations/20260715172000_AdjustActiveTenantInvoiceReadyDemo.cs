using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715172000_AdjustActiveTenantInvoiceReadyDemo")]
    public partial class AdjustActiveTenantInvoiceReadyDemo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
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
                    now_utc timestamptz := now();
                    active_tenant_id uuid;
                    primary_landlord_id uuid;
                    active_contract_id uuid;
                    active_request_id uuid;
                    active_deposit_id uuid;
                    room_b201_id uuid;
                    active_wallet_id uuid;
                    landlord_wallet_id uuid;
                    june_invoice_id uuid;
                    wrong_invoice_id uuid;
                    current_invoice_id uuid;
                    electric_service_id uuid;
                    water_service_id uuid;
                    internet_service_id uuid;
                    reading_electric_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-ELECTRIC-202607');
                    reading_water_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-WATER-202607');
                BEGIN
                    SELECT id INTO active_tenant_id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1;
                    SELECT id INTO primary_landlord_id FROM users WHERE normalized_email = 'NGUYENXUANHUAN21102005@GMAIL.COM' LIMIT 1;
                    SELECT id, rental_request_id, room_deposit_id, room_id
                    INTO active_contract_id, active_request_id, active_deposit_id, room_b201_id
                    FROM contracts
                    WHERE contract_number = 'DEMO-FLOW-ACTIVE-B201-20260601'
                    LIMIT 1;

                    IF active_tenant_id IS NULL OR active_contract_id IS NULL THEN
                        RAISE NOTICE 'AdjustActiveTenantInvoiceReadyDemo skipped because active demo tenant/contract is missing.';
                        RETURN;
                    END IF;

                    SELECT id INTO active_wallet_id FROM wallet_accounts WHERE user_id = active_tenant_id LIMIT 1;
                    SELECT id INTO landlord_wallet_id FROM wallet_accounts WHERE user_id = primary_landlord_id LIMIT 1;
                    SELECT id INTO june_invoice_id FROM invoices WHERE invoice_no = 'DEMO-FLOW-INV-202606-PAID' LIMIT 1;
                    SELECT id INTO wrong_invoice_id FROM invoices WHERE invoice_no = 'DEMO-FLOW-INV-202607-WRONG' LIMIT 1;
                    SELECT id INTO current_invoice_id
                    FROM invoices
                    WHERE invoice_no IN ('DEMO-FLOW-INV-202607-CORRECT', 'DEMO-FLOW-INV-202607-CURRENT')
                      AND contract_id = active_contract_id
                    ORDER BY CASE WHEN invoice_no = 'DEMO-FLOW-INV-202607-CURRENT' THEN 0 ELSE 1 END
                    LIMIT 1;

                    SELECT id INTO electric_service_id FROM billing_service_types WHERE lower(name) = lower('Điện') LIMIT 1;
                    SELECT id INTO water_service_id FROM billing_service_types WHERE lower(name) = lower('Nước') LIMIT 1;
                    SELECT id INTO internet_service_id FROM billing_service_types WHERE lower(name) IN (lower('Internet'), lower('Wifi')) LIMIT 1;

                    DELETE FROM viewing_appointments
                    WHERE tenant_user_id = active_tenant_id
                       OR created_by_user_id = active_tenant_id;

                    UPDATE rooms
                    SET max_occupants = 2,
                        is_tiered_pricing = TRUE,
                        status = 'Occupied',
                        description = 'Phòng B201 cho tối đa 2 người; hợp đồng demo hiện có 1 người ở.',
                        updated_at = now_utc
                    WHERE id = room_b201_id;

                    INSERT INTO room_price_tiers (id, room_id, occupant_count, monthly_rent, is_active, created_at, updated_at)
                    VALUES
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-B201-1'), room_b201_id, 1, 3600000, TRUE, TIMESTAMPTZ '2026-07-15 00:00:00Z', now_utc),
                        (pg_temp.demo_flow_uuid('DEMO-FLOW-TIER-B201-2'), room_b201_id, 2, 3950000, TRUE, TIMESTAMPTZ '2026-07-15 00:00:00Z', now_utc)
                    ON CONFLICT (id) DO UPDATE SET
                        occupant_count = EXCLUDED.occupant_count,
                        monthly_rent = EXCLUDED.monthly_rent,
                        is_active = TRUE,
                        updated_at = now_utc;

                    UPDATE rental_requests
                    SET expected_occupant_count = 1,
                        monthly_rent_snapshot = 3600000,
                        deposit_amount_snapshot = 3600000,
                        tenant_note = 'DEMO-FLOW: request 1 người đã accept để có hợp đồng active quá khứ.',
                        updated_at = now_utc
                    WHERE id = active_request_id;

                    UPDATE room_deposits
                    SET deposit_amount = 3600000,
                        note = 'DEMO-FLOW: active tenant 1 người đã thanh toán cọc; nếu chấm dứt trước hạn thì cọc chuyển về ví chủ trọ.',
                        updated_at = now_utc
                    WHERE id = active_deposit_id;

                    UPDATE contracts
                    SET monthly_rent = 3600000,
                        deposit_amount = 3600000,
                        room_snapshot = jsonb_set(
                            jsonb_set(COALESCE(room_snapshot, '{}'::jsonb), '{MaxOccupants}', '2'::jsonb, TRUE),
                            '{OccupantCount}',
                            '1'::jsonb,
                            TRUE),
                        updated_at = now_utc
                    WHERE id = active_contract_id;

                    UPDATE wallet_accounts
                    SET balance = 50000000,
                        reserved_balance = 0,
                        status = 'Active',
                        updated_at = now_utc
                    WHERE id = active_wallet_id;

                    UPDATE wallet_accounts
                    SET reserved_balance = 3600000,
                        updated_at = now_utc
                    WHERE id = landlord_wallet_id;

                    UPDATE meter_readings
                    SET proof_image_object_key = 'demo-flow/meters/b201-electric-202607.svg',
                        previous_reading = 1250,
                        current_reading = 1341,
                        consumption = 91,
                        ai_reading = 1341,
                        ai_raw_text = 'AI OCR: điện hiện tại 1341 kWh',
                        was_manually_corrected = FALSE,
                        updated_at = now_utc
                    WHERE id = reading_electric_id;

                    UPDATE meter_readings
                    SET proof_image_object_key = 'demo-flow/meters/b201-water-202607.svg',
                        previous_reading = 88,
                        current_reading = 96,
                        consumption = 8,
                        ai_reading = 96,
                        ai_raw_text = 'AI OCR: nước hiện tại 96 m3',
                        was_manually_corrected = FALSE,
                        updated_at = now_utc
                    WHERE id = reading_water_id;

                    DELETE FROM invoice_items WHERE invoice_id = wrong_invoice_id;
                    DELETE FROM invoices WHERE id = wrong_invoice_id;

                    UPDATE invoices
                    SET rent_amount = 3600000,
                        utility_amount = 520000,
                        service_amount = 160000,
                        discount_amount = 0,
                        total_amount = 4280000,
                        status = 'Paid',
                        note = 'Hóa đơn tháng 6 đã thanh toán, dùng làm lịch sử giao dịch.',
                        updated_at = now_utc
                    WHERE id = june_invoice_id;

                    UPDATE invoice_items
                    SET description = 'Tiền phòng tháng 06/2026 - hợp đồng 1 người',
                        quantity = 1,
                        unit_price = 3600000,
                        amount = 3600000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JUNE-RENT');

                    UPDATE invoices
                    SET invoice_no = 'DEMO-FLOW-INV-202607-CURRENT',
                        billing_period_start = DATE '2026-07-01',
                        billing_period_end = DATE '2026-07-31',
                        issue_date = DATE '2026-07-15',
                        due_date = DATE '2026-07-25',
                        rent_amount = 3600000,
                        utility_amount = 508000,
                        service_amount = 120000,
                        discount_amount = 0,
                        total_amount = 4228000,
                        status = 'Issued',
                        note = 'Hóa đơn hiện tại của hợp đồng 1 người; ảnh điện/nước khớp chỉ số 1341 kWh và 96 m3. Ví tenant có sẵn 50.000.000 để thanh toán ngay.',
                        sent_at = TIMESTAMPTZ '2026-07-15 09:00:00Z',
                        paid_at = NULL,
                        cancelled_at = NULL,
                        cancel_reason = NULL,
                        wallet_transfer_group_id = NULL,
                        updated_at = now_utc
                    WHERE id = current_invoice_id;

                    UPDATE invoice_items
                    SET description = 'Tiền phòng tháng 07/2026 - hợp đồng 1 người',
                        quantity = 1,
                        unit_price = 3600000,
                        amount = 3600000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-RENT');

                    UPDATE invoice_items
                    SET service_type_id = electric_service_id,
                        meter_reading_id = reading_electric_id,
                        description = 'Điện tháng 07/2026: 1341 - 1250 = 91 kWh',
                        quantity = 91,
                        unit_price = 4000,
                        amount = 364000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-ELECTRIC');

                    UPDATE invoice_items
                    SET service_type_id = water_service_id,
                        meter_reading_id = reading_water_id,
                        description = 'Nước tháng 07/2026: 96 - 88 = 8 m3',
                        quantity = 8,
                        unit_price = 18000,
                        amount = 144000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-WATER');

                    UPDATE invoice_items
                    SET service_type_id = internet_service_id,
                        description = 'Internet + rác tháng 07/2026',
                        quantity = 1,
                        unit_price = 120000,
                        amount = 120000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-SERVICE');

                    UPDATE payment_transactions
                    SET amount = 54280000,
                        gateway_response_message = 'Seed ví active tenant đủ tiền: sau hóa đơn tháng 6 còn 50.000.000 để trả hóa đơn hiện tại.',
                        updated_at = now_utc
                    WHERE idempotency_key = 'demo-flow:active-tenant-seed-topup';

                    UPDATE wallet_transactions
                    SET amount = 54280000,
                        balance_after = 54280000,
                        description = 'DEMO-FLOW: Lê Quang Linh có sẵn 50.000.000 sau lịch sử hóa đơn tháng 6.'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-ACTIVE-SEED');

                    UPDATE wallet_transactions
                    SET amount = 4280000,
                        balance_before = 54280000,
                        balance_after = 50000000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-JUNE-TENANT');

                    UPDATE wallet_transactions
                    SET amount = 4280000,
                        balance_before = 8220000,
                        balance_after = 12500000,
                        reserved_balance_before = 3600000,
                        reserved_balance_after = 3600000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-JUNE-LANDLORD');
                END $demo$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE wallet_accounts
                SET balance = 3670000,
                    updated_at = now()
                WHERE user_id = (
                    SELECT id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1
                );
                """);
        }
    }
}
