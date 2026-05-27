-- Reset data user thuong, giu lai admin
-- Thu tu xoa: con truoc, cha sau (tranh vi pham FK)

DELETE FROM kyc_verifications;
DELETE FROM login_logs;
DELETE FROM user_tokens;
DELETE FROM external_logins;
DELETE FROM user_profiles;
DELETE FROM approval_audit_logs;
DELETE FROM property_images;
DELETE FROM lease_policies;
DELETE FROM rooming_house_amenities;
DELETE FROM room_amenities;
DELETE FROM room_price_tiers;
DELETE FROM rooms;
DELETE FROM rooming_house_legal_documents;
DELETE FROM rooming_houses;

-- Xoa user_roles cua user thuong (giu lai Admin)
DELETE FROM user_roles
WHERE user_id NOT IN (
  SELECT u.id FROM users u
  JOIN user_roles ur ON ur.user_id = u.id
  JOIN roles r ON r.id = ur.role_id
  WHERE r.name = 'Admin'
);

-- Xoa users thuong (giu lai Admin)
DELETE FROM users
WHERE id NOT IN (
  SELECT u.id FROM users u
  JOIN user_roles ur ON ur.user_id = u.id
  JOIN roles r ON r.id = ur.role_id
  WHERE r.name = 'Admin'
);

-- Ket qua
SELECT 'DONE - Da xoa xong!' AS result;
SELECT u.email, r.name AS role FROM users u
JOIN user_roles ur ON ur.user_id = u.id
JOIN roles r ON r.id = ur.role_id;
