BEGIN;

-- Hai tài khoản người thuê chính dùng để tạo hợp đồng cho các phòng còn trống.
INSERT INTO users (
    id, email, normalized_email, phone_number, display_name, status,
    onboarding_status, email_confirmed, phone_confirmed, access_failed_count,
    created_at, updated_at)
VALUES
    ('71000000-0000-0000-0000-000000000102', 'tenant.kfc102@example.test', 'TENANT.KFC102@EXAMPLE.TEST', '0901102102', 'Trần Minh Anh', 'Active', 'Completed', true, true, 0, now(), now()),
    ('71000000-0000-0000-0000-000000000201', 'tenant.kfc201@example.test', 'TENANT.KFC201@EXAMPLE.TEST', '0901102201', 'Nguyễn Hoàng Nam', 'Active', 'Completed', true, true, 0, now(), now())
ON CONFLICT (id) DO UPDATE SET
    display_name = EXCLUDED.display_name, status = 'Active', onboarding_status = 'Completed',
    deleted_at = NULL, updated_at = now();

INSERT INTO user_profiles (
    user_id, full_name, date_of_birth, gender, address_line,
    emergency_contact_name, emergency_contact_phone, created_at, updated_at)
VALUES
    ('71000000-0000-0000-0000-000000000102', 'Trần Minh Anh', '1998-03-14', 'Female', 'Đà Nẵng', 'Trần Văn Minh', '0909000102', now(), now()),
    ('71000000-0000-0000-0000-000000000201', 'Nguyễn Hoàng Nam', '1996-08-22', 'Male', 'Đà Nẵng', 'Nguyễn Thị Hạnh', '0909000201', now(), now())
ON CONFLICT (user_id) DO UPDATE SET full_name = EXCLUDED.full_name, updated_at = now();

INSERT INTO user_roles (user_id, role_id, created_at)
VALUES
    ('71000000-0000-0000-0000-000000000102', 2, now()),
    ('71000000-0000-0000-0000-000000000201', 2, now())
ON CONFLICT DO NOTHING;

-- Yêu cầu thuê đã được chủ trọ duyệt.
INSERT INTO rental_requests (
    id, room_id, tenant_user_id, approved_by_landlord_id,
    desired_start_date, expected_end_date, expected_occupant_count,
    monthly_rent_snapshot, deposit_amount_snapshot, tenant_note,
    status, responded_at, created_at, updated_at)
VALUES
    ('72000000-0000-0000-0000-000000000102', 'c967a22e-e632-3942-0c02-46bbd3a8e6e0', '71000000-0000-0000-0000-000000000102', '989ed540-3ffd-6859-4445-20ce1b268bc7',
     '2026-06-01', '2027-05-31', 3, 3500000, 3500000, 'Nhóm 3 người thuê phòng KFC-102 để kiểm thử tạo hóa đơn.',
     'Accepted', '2026-05-25 09:00:00+07', '2026-05-24 09:00:00+07', now()),
    ('72000000-0000-0000-0000-000000000201', 'fdf9804e-b187-f1bc-448a-b1c91c8ee4b5', '71000000-0000-0000-0000-000000000201', '989ed540-3ffd-6859-4445-20ce1b268bc7',
     '2026-06-01', '2027-05-31', 2, 3000000, 3000000, 'Nhóm 2 người thuê phòng KFC-201 để kiểm thử tạo hóa đơn.',
     'Accepted', '2026-05-25 10:00:00+07', '2026-05-24 10:00:00+07', now())
ON CONFLICT (id) DO UPDATE SET status = 'Accepted', responded_at = EXCLUDED.responded_at, updated_at = now();

-- Cọc đã thanh toán để hợp đồng có thể ở trạng thái Active.
INSERT INTO room_deposits (
    id, rental_request_id, room_id, tenant_user_id, landlord_user_id,
    deposit_amount, currency, status, payment_deadline_at, paid_at, note,
    created_at, updated_at)
VALUES
    ('73000000-0000-0000-0000-000000000102', '72000000-0000-0000-0000-000000000102', 'c967a22e-e632-3942-0c02-46bbd3a8e6e0', '71000000-0000-0000-0000-000000000102', '989ed540-3ffd-6859-4445-20ce1b268bc7',
     3500000, 'VND', 'Paid', '2026-05-28 23:59:00+07', '2026-05-26 09:00:00+07', 'Dữ liệu cọc phục vụ kiểm thử hóa đơn.', now(), now()),
    ('73000000-0000-0000-0000-000000000201', '72000000-0000-0000-0000-000000000201', 'fdf9804e-b187-f1bc-448a-b1c91c8ee4b5', '71000000-0000-0000-0000-000000000201', '989ed540-3ffd-6859-4445-20ce1b268bc7',
     3000000, 'VND', 'Paid', '2026-05-28 23:59:00+07', '2026-05-26 10:00:00+07', 'Dữ liệu cọc phục vụ kiểm thử hóa đơn.', now(), now())
ON CONFLICT (id) DO UPDATE SET status = 'Paid', paid_at = EXCLUDED.paid_at, updated_at = now();

-- Hợp đồng đã ký và đang hiệu lực, chưa có hóa đơn để có thể test ngay.
INSERT INTO contracts (
    id, rental_request_id, room_deposit_id, room_id, main_tenant_user_id,
    contract_number, start_date, end_date, monthly_rent, deposit_amount,
    payment_day, status, room_snapshot, activated_at, created_at, updated_at)
VALUES
    ('74000000-0000-0000-0000-000000000102', '72000000-0000-0000-0000-000000000102', '73000000-0000-0000-0000-000000000102', 'c967a22e-e632-3942-0c02-46bbd3a8e6e0', '71000000-0000-0000-0000-000000000102',
     'KFC-102-20260601', '2026-06-01', '2027-05-31', 3500000, 3500000, 5, 'Active',
     '{"RoomNumber":"KFC-102","RoomingHouseName":"Khu trọ KFC Riverside","MaxOccupants":3}'::jsonb,
     '2026-05-27 15:00:00+07', '2026-05-27 09:00:00+07', now()),
    ('74000000-0000-0000-0000-000000000201', '72000000-0000-0000-0000-000000000201', '73000000-0000-0000-0000-000000000201', 'fdf9804e-b187-f1bc-448a-b1c91c8ee4b5', '71000000-0000-0000-0000-000000000201',
     'KFC-201-20260601', '2026-06-01', '2027-05-31', 3000000, 3000000, 5, 'Active',
     '{"RoomNumber":"KFC-201","RoomingHouseName":"Khu trọ KFC Riverside","MaxOccupants":2}'::jsonb,
     '2026-05-27 16:00:00+07', '2026-05-27 10:00:00+07', now())
ON CONFLICT (id) DO UPDATE SET status = 'Active', activated_at = EXCLUDED.activated_at, deleted_at = NULL, updated_at = now();

INSERT INTO contract_occupants (
    id, contract_id, user_id, full_name, phone_number, date_of_birth,
    relationship_to_main_tenant, move_in_date, status, created_at, updated_at)
VALUES
    ('75000000-0000-0000-0000-000000000102', '74000000-0000-0000-0000-000000000102', '71000000-0000-0000-0000-000000000102', 'Trần Minh Anh', '0901102102', '1998-03-14', 'Self', '2026-06-01', 'Active', now(), now()),
    ('75000000-0000-0000-0000-000000000103', '74000000-0000-0000-0000-000000000102', NULL, 'Lê Ngọc Hân', '0901102103', '1999-07-09', 'Bạn cùng phòng', '2026-06-01', 'Active', now(), now()),
    ('75000000-0000-0000-0000-000000000104', '74000000-0000-0000-0000-000000000102', NULL, 'Phạm Gia Bảo', '0901102104', '1997-11-18', 'Bạn cùng phòng', '2026-06-01', 'Active', now(), now()),
    ('75000000-0000-0000-0000-000000000201', '74000000-0000-0000-0000-000000000201', '71000000-0000-0000-0000-000000000201', 'Nguyễn Hoàng Nam', '0901102201', '1996-08-22', 'Self', '2026-06-01', 'Active', now(), now()),
    ('75000000-0000-0000-0000-000000000202', '74000000-0000-0000-0000-000000000201', NULL, 'Võ Thanh Tùng', '0901102202', '1995-12-02', 'Bạn cùng phòng', '2026-06-01', 'Active', now(), now())
ON CONFLICT (id) DO UPDATE SET
    full_name = EXCLUDED.full_name, relationship_to_main_tenant = EXCLUDED.relationship_to_main_tenant,
    status = 'Active', move_out_date = NULL, updated_at = now();

INSERT INTO contract_signatures (
    id, contract_id, signer_user_id, signer_role, signature_method,
    signature_text, signed_at, ip_address, user_agent, created_at)
VALUES
    ('76000000-0000-0000-0000-000000000102', '74000000-0000-0000-0000-000000000102', '989ed540-3ffd-6859-4445-20ce1b268bc7', 'Landlord', 'EmailOtp', 'Chủ trọ đã ký hợp đồng KFC-102.', '2026-05-27 14:00:00+07', '127.0.0.1', 'Demo invoice fixture', now()),
    ('76000000-0000-0000-0000-000000000103', '74000000-0000-0000-0000-000000000102', '71000000-0000-0000-0000-000000000102', 'Tenant', 'EmailOtp', 'Trần Minh Anh đã ký hợp đồng KFC-102.', '2026-05-27 15:00:00+07', '127.0.0.1', 'Demo invoice fixture', now()),
    ('76000000-0000-0000-0000-000000000201', '74000000-0000-0000-0000-000000000201', '989ed540-3ffd-6859-4445-20ce1b268bc7', 'Landlord', 'EmailOtp', 'Chủ trọ đã ký hợp đồng KFC-201.', '2026-05-27 15:00:00+07', '127.0.0.1', 'Demo invoice fixture', now()),
    ('76000000-0000-0000-0000-000000000202', '74000000-0000-0000-0000-000000000201', '71000000-0000-0000-0000-000000000201', 'Tenant', 'EmailOtp', 'Nguyễn Hoàng Nam đã ký hợp đồng KFC-201.', '2026-05-27 16:00:00+07', '127.0.0.1', 'Demo invoice fixture', now())
ON CONFLICT (id) DO NOTHING;

UPDATE rooms
SET status = 'Occupied', updated_at = now()
WHERE id IN (
    'c967a22e-e632-3942-0c02-46bbd3a8e6e0',
    'fdf9804e-b187-f1bc-448a-b1c91c8ee4b5'
);

COMMIT;
