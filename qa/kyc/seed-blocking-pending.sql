-- =============================================================================
-- KYC QA seed: User B already has PendingAdminReview (Validation Block Path 1)
-- Prerequisite: run seed-test-users.sql first
-- =============================================================================

BEGIN;

DELETE FROM kyc_verifications
WHERE user_id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

INSERT INTO kyc_verifications (
    id, user_id, document_type, ekyc_provider,
    front_image_object_key, back_image_object_key, selfie_image_object_key,
    selfie_capture_method, ekyc_result, risk_level, status,
    submitted_at, created_at, updated_at
)
VALUES (
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01',
    'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    'CCCD',
    'VNPT',
    'kyc/seed/front.jpg',
    'kyc/seed/back.jpg',
    'kyc/seed/selfie.jpg',
    'Webcam',
    'Passed',
    'Low',
    'PendingAdminReview',
    NOW(),
    NOW(),
    NOW()
);

COMMIT;

SELECT id, user_id, status, ekyc_result FROM kyc_verifications
WHERE user_id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
