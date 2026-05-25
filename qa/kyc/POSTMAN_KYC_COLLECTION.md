# KYC API — Postman / Swagger manual test pack

**Base URL:** `http://localhost:5294`  
**Dev auth header:** `X-Dev-User-Id: <uuid>` (Development only)

## Form field names (must match exactly)

| Key | Type | Example |
|-----|------|---------|
| DocumentType | text | `CCCD` or `Passport` |
| SelfieCaptureMethod | text | `Webcam`, `MobileCamera`, `Upload` |
| FrontImage | file | `front.jpg` |
| BackImage | file | `back.jpg` |
| SelfieImage | file | `selfie.jpg` |

---

## TC-01 Happy Path

**POST** `/api/kyc/submissions`  
**Headers:** `X-Dev-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`

**Expected HTTP:** `200`

```json
{
  "success": true,
  "message": "Submission received. Your profile is pending admin review.",
  "data": {
    "kycId": "<guid>",
    "status": "PendingAdminReview",
    "ekycResult": "Passed",
    "riskLevel": "Low",
    "documentType": "CCCD",
    "ocrFullName": "Nguyen Van A",
    "ocrCitizenIdMasked": "********9012",
    "ocrDateOfBirth": "2000-01-15T00:00:00",
    "ocrGender": "Male",
    "ocrConfidence": 0.96,
    "documentCheckResult": "Valid",
    "faceMatchScore": 0.92,
    "faceMatchResult": "Matched",
    "livenessResult": "Passed",
    "submittedAt": "<utc>",
    "message": "Submission received. Your profile is pending admin review."
  }
}
```

**DB verify:**

```sql
SELECT status, ekyc_result, risk_level, citizen_id_hash, ocr_citizen_id_masked
FROM kyc_verifications
WHERE user_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
ORDER BY created_at DESC LIMIT 1;
-- status = PendingAdminReview, ekyc_result = Passed, citizen_id_hash NOT NULL, masked ID present
```

---

## TC-02 Block — PendingAdminReview

**Precondition:** Happy path already run for User A, OR run `seed-blocking-pending.sql` for User B.

**POST** `/api/kyc/submissions` (same body as TC-01)  
**Headers:** `X-Dev-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`

**Expected HTTP:** `400`

```json
{
  "success": false,
  "message": "A KYC submission is already pending admin review.",
  "code": "KYC_PENDING_ADMIN_REVIEW",
  "details": null
}
```

---

## TC-03 Block — Missing BackImage

**POST** `/api/kyc/submissions`  
**Headers:** `X-Dev-User-Id: cccccccc-cccc-cccc-cccc-cccccccccccc`  
**Form:** DocumentType, SelfieCaptureMethod, FrontImage, SelfieImage only (omit BackImage)

**Expected HTTP:** `400`

```json
{
  "success": false,
  "message": "Back image is required.",
  "code": "BACK_IMAGE_REQUIRED",
  "details": null
}
```

---

## TC-04 Unauthorized

**GET** `/api/kyc/my-status` (no header, no query userId)

**Expected HTTP:** `401`

```json
{
  "success": false,
  "message": "Authentication required.",
  "code": "UNAUTHORIZED",
  "details": null
}
```

---

## TC-05 GET my-status

**GET** `/api/kyc/my-status`  
**Headers:** `X-Dev-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`

**Expected HTTP:** `200`

```json
{
  "success": true,
  "message": "Success",
  "data": {
    "hasSubmission": true,
    "kycId": "<guid>",
    "status": "PendingAdminReview",
    "ekycResult": "Passed",
    "riskLevel": "Low",
    "documentType": "CCCD",
    "ocrFullName": "Nguyen Van A",
    "ocrCitizenIdMasked": "********9012",
    "faceMatchScore": 0.92,
    "livenessResult": "Passed",
    "submittedAt": "<utc>",
    "reviewedAt": null,
    "rejected_reason": null
  }
}
```

---

## TC-06 GET my-history

**GET** `/api/kyc/my-history`  
**Headers:** `X-Dev-User-Id: aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`

**Expected HTTP:** `200` — `data` is a **JSON array** of history items.

---

## Mock VNPT failure hooks (optional)

Rename fixture file before upload so object key contains:

- `fail-document` in path → `EKYC_DOCUMENT_FAILED` (400, no row)
- `fail-provider` in path → row with `EkycFailed` / `ProviderError`

Example curl:

```bash
cp qa/kyc/fixtures/front.jpg qa/kyc/fixtures/fail-document-front.jpg
# Upload fail-document-front.jpg as FrontImage
```
