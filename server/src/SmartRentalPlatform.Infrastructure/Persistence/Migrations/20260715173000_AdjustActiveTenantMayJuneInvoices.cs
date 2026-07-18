using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace SmartRentalPlatform.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260715173000_AdjustActiveTenantMayJuneInvoices")]
    public partial class AdjustActiveTenantMayJuneInvoices : Migration
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
                    may_invoice_id uuid;
                    june_invoice_id uuid;
                    active_wallet_id uuid;
                    landlord_wallet_id uuid;
                    reading_electric_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-ELECTRIC-202607');
                    reading_water_id uuid := pg_temp.demo_flow_uuid('DEMO-FLOW-METER-WATER-202607');
                BEGIN
                    SELECT id INTO active_tenant_id FROM users WHERE normalized_email = 'HOCTIENGANH4ENGLISH@GMAIL.COM' LIMIT 1;
                    SELECT id INTO primary_landlord_id FROM users WHERE normalized_email = 'NGUYENXUANHUAN21102005@GMAIL.COM' LIMIT 1;
                    SELECT id, rental_request_id, room_deposit_id
                    INTO active_contract_id, active_request_id, active_deposit_id
                    FROM contracts
                    WHERE contract_number = 'DEMO-FLOW-ACTIVE-B201-20260601'
                    LIMIT 1;

                    IF active_tenant_id IS NULL OR active_contract_id IS NULL THEN
                        RAISE NOTICE 'AdjustActiveTenantMayJuneInvoices skipped because active demo tenant/contract is missing.';
                        RETURN;
                    END IF;

                    SELECT id INTO active_wallet_id FROM wallet_accounts WHERE user_id = active_tenant_id LIMIT 1;
                    SELECT id INTO landlord_wallet_id FROM wallet_accounts WHERE user_id = primary_landlord_id LIMIT 1;
                    SELECT id INTO may_invoice_id
                    FROM invoices
                    WHERE contract_id = active_contract_id
                      AND invoice_no IN ('DEMO-FLOW-INV-202606-PAID', 'HD-B201-202605')
                    LIMIT 1;
                    SELECT id INTO june_invoice_id
                    FROM invoices
                    WHERE contract_id = active_contract_id
                      AND invoice_no IN ('DEMO-FLOW-INV-202607-CURRENT', 'DEMO-FLOW-INV-202607-CORRECT', 'HD-B201-202606')
                    LIMIT 1;

                    DELETE FROM invoice_items
                    WHERE invoice_id IN (
                        SELECT id
                        FROM invoices
                        WHERE contract_id = active_contract_id
                          AND status <> 'Draft'
                          AND id NOT IN (COALESCE(may_invoice_id, '00000000-0000-0000-0000-000000000000'::uuid), COALESCE(june_invoice_id, '00000000-0000-0000-0000-000000000000'::uuid))
                    );

                    DELETE FROM invoices
                    WHERE contract_id = active_contract_id
                      AND status <> 'Draft'
                      AND id NOT IN (COALESCE(may_invoice_id, '00000000-0000-0000-0000-000000000000'::uuid), COALESCE(june_invoice_id, '00000000-0000-0000-0000-000000000000'::uuid));

                    UPDATE rental_requests
                    SET desired_start_date = DATE '2026-05-01',
                        expected_end_date = DATE '2027-04-30',
                        expected_occupant_count = 1,
                        monthly_rent_snapshot = 3600000,
                        deposit_amount_snapshot = 3600000,
                        tenant_note = 'Yêu cầu thuê phòng B201 cho 1 người ở, bắt đầu từ tháng 05/2026.',
                        updated_at = now_utc
                    WHERE id = active_request_id;

                    UPDATE room_deposits
                    SET deposit_amount = 3600000,
                        payment_deadline_at = TIMESTAMPTZ '2026-04-28 23:59:00Z',
                        paid_at = TIMESTAMPTZ '2026-04-25 10:00:00Z',
                        note = 'Tiền cọc phòng B201 đã thanh toán. Nếu chấm dứt trước hạn, tiền cọc được chuyển cho chủ trọ theo điều khoản hợp đồng.',
                        updated_at = now_utc
                    WHERE id = active_deposit_id;

                    UPDATE contracts
                    SET start_date = DATE '2026-05-01',
                        end_date = DATE '2027-04-30',
                        monthly_rent = 3600000,
                        deposit_amount = 3600000,
                        activated_at = TIMESTAMPTZ '2026-04-25 15:00:00Z',
                        created_at = TIMESTAMPTZ '2026-04-25 09:00:00Z',
                        room_snapshot = jsonb_set(
                            jsonb_set(COALESCE(room_snapshot, '{}'::jsonb), '{MaxOccupants}', '2'::jsonb, TRUE),
                            '{OccupantCount}',
                            '1'::jsonb,
                            TRUE),
                        updated_at = now_utc
                    WHERE id = active_contract_id;

                    UPDATE contract_occupants
                    SET move_in_date = DATE '2026-05-01',
                        updated_at = now_utc
                    WHERE contract_id = active_contract_id
                      AND user_id = active_tenant_id;

                    UPDATE meter_readings
                    SET billing_period_start = DATE '2026-06-01',
                        billing_period_end = DATE '2026-06-30',
                        proof_image_object_key = 'demo-flow/meters/b201-electric-202606.png',
                        previous_reading = 1250,
                        current_reading = 1341,
                        consumption = 91,
                        ai_reading = 1341,
                        ai_raw_text = 'AI OCR: điện hiện tại 1341 kWh',
                        reading_at = TIMESTAMPTZ '2026-06-30 08:00:00Z',
                        updated_at = now_utc
                    WHERE id = reading_electric_id;

                    UPDATE meter_readings
                    SET billing_period_start = DATE '2026-06-01',
                        billing_period_end = DATE '2026-06-30',
                        proof_image_object_key = 'demo-flow/meters/b201-water-202606.png',
                        previous_reading = 88,
                        current_reading = 96,
                        consumption = 8,
                        ai_reading = 96,
                        ai_raw_text = 'AI OCR: nước hiện tại 96 m3',
                        reading_at = TIMESTAMPTZ '2026-06-30 08:05:00Z',
                        updated_at = now_utc
                    WHERE id = reading_water_id;

                    UPDATE invoices
                    SET invoice_no = 'HD-B201-202605',
                        billing_period_start = DATE '2026-05-01',
                        billing_period_end = DATE '2026-05-31',
                        issue_date = DATE '2026-05-31',
                        due_date = DATE '2026-06-05',
                        rent_amount = 3600000,
                        utility_amount = 520000,
                        service_amount = 160000,
                        discount_amount = 0,
                        total_amount = 4280000,
                        status = 'Paid',
                        note = 'Hóa đơn tháng 05/2026 đã thanh toán.',
                        sent_at = TIMESTAMPTZ '2026-05-31 08:00:00Z',
                        paid_at = TIMESTAMPTZ '2026-06-01 09:00:00Z',
                        wallet_transfer_group_id = pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-MAY'),
                        created_at = TIMESTAMPTZ '2026-05-31 08:00:00Z',
                        updated_at = now_utc
                    WHERE id = may_invoice_id;

                    UPDATE invoice_items
                    SET description = 'Tiền phòng tháng 05/2026 - hợp đồng 1 người',
                        quantity = 1,
                        unit_price = 3600000,
                        amount = 3600000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JUNE-RENT');

                    UPDATE invoice_items
                    SET description = 'Internet + rác tháng 05/2026',
                        quantity = 1,
                        unit_price = 160000,
                        amount = 160000
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JUNE-SERVICE');

                    UPDATE invoices
                    SET invoice_no = 'HD-B201-202606',
                        billing_period_start = DATE '2026-06-01',
                        billing_period_end = DATE '2026-06-30',
                        issue_date = DATE '2026-06-30',
                        due_date = DATE '2026-07-05',
                        rent_amount = 3600000,
                        utility_amount = 508000,
                        service_amount = 120000,
                        discount_amount = 0,
                        total_amount = 4228000,
                        status = 'Overdue',
                        note = 'Hóa đơn tháng 06/2026 đã quá hạn thanh toán.',
                        sent_at = TIMESTAMPTZ '2026-06-30 09:00:00Z',
                        paid_at = NULL,
                        cancelled_at = NULL,
                        cancel_reason = NULL,
                        wallet_transfer_group_id = NULL,
                        created_at = TIMESTAMPTZ '2026-06-30 09:00:00Z',
                        updated_at = now_utc
                    WHERE id = june_invoice_id;

                    UPDATE invoice_items
                    SET description = 'Tiền phòng tháng 06/2026 - hợp đồng 1 người',
                        quantity = 1,
                        unit_price = 3600000,
                        amount = 3600000,
                        item_type = 'Rent'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-RENT');

                    UPDATE invoice_items
                    SET description = 'Điện tháng 06/2026: 1341 - 1250 = 91 kWh',
                        quantity = 91,
                        unit_price = 4000,
                        amount = 364000,
                        item_type = 'Service',
                        meter_reading_id = reading_electric_id
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-ELECTRIC');

                    UPDATE invoice_items
                    SET description = 'Nước tháng 06/2026: 96 - 88 = 8 m3',
                        quantity = 8,
                        unit_price = 18000,
                        amount = 144000,
                        item_type = 'Service',
                        meter_reading_id = reading_water_id
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-WATER');

                    UPDATE invoice_items
                    SET description = 'Internet + rác tháng 06/2026',
                        quantity = 1,
                        unit_price = 120000,
                        amount = 120000,
                        item_type = 'Service'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-ITEM-JULY-CORRECT-SERVICE');

                    UPDATE wallet_accounts
                    SET balance = 50000000,
                        reserved_balance = 0,
                        updated_at = now_utc
                    WHERE id = active_wallet_id;

                    UPDATE wallet_accounts
                    SET reserved_balance = 3600000,
                        updated_at = now_utc
                    WHERE id = landlord_wallet_id;

                    UPDATE payment_transactions
                    SET amount = 54280000,
                        gateway_response_message = 'Nạp ví ban đầu cho Lê Quang Linh.',
                        paid_at = TIMESTAMPTZ '2026-06-01 09:00:00Z',
                        confirmed_at = TIMESTAMPTZ '2026-06-01 09:00:00Z',
                        created_at = TIMESTAMPTZ '2026-06-01 09:00:00Z',
                        updated_at = now_utc
                    WHERE idempotency_key = 'demo-flow:active-tenant-seed-topup';

                    UPDATE wallet_transactions
                    SET amount = 54280000,
                        balance_before = 0,
                        balance_after = 54280000,
                        description = 'Nạp ví ban đầu.',
                        created_at = TIMESTAMPTZ '2026-06-01 09:00:00Z'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-ACTIVE-SEED');

                    UPDATE wallet_transactions
                    SET transfer_group_id = pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-MAY'),
                        amount = 4280000,
                        balance_before = 54280000,
                        balance_after = 50000000,
                        related_entity_id = may_invoice_id,
                        description = 'Thanh toán hóa đơn tháng 05/2026 phòng B201.',
                        created_at = TIMESTAMPTZ '2026-06-01 09:00:00Z'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-JUNE-TENANT');

                    UPDATE wallet_transactions
                    SET transfer_group_id = pg_temp.demo_flow_uuid('DEMO-FLOW-TG-INVOICE-MAY'),
                        amount = 4280000,
                        balance_before = 8220000,
                        balance_after = 12500000,
                        reserved_balance_before = 3600000,
                        reserved_balance_after = 3600000,
                        related_entity_id = may_invoice_id,
                        description = 'Nhận thanh toán hóa đơn tháng 05/2026 phòng B201.',
                        created_at = TIMESTAMPTZ '2026-06-01 09:00:00Z'
                    WHERE id = pg_temp.demo_flow_uuid('DEMO-FLOW-WTX-JUNE-LANDLORD');
                END $demo$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE invoices
                SET invoice_no = 'DEMO-FLOW-INV-202607-CURRENT'
                WHERE invoice_no = 'HD-B201-202606';
                """);
        }
    }
}
