# KYC VNPT Structure Integration Plan

Tài liệu này dùng để chuẩn hóa phần KYC/VNPT từ PR #5 trước khi merge vào code hiện tại gồm Auth flow + PR #4 profile logic.

## 1. Mục Tiêu

Tích hợp logic KYC dùng VNPT eKYC vào cấu trúc hiện tại mà không kéo nguyên PR #5.

Không lấy nguyên:

- `Program.cs` của PR #5.
- Frontend root cũ của PR #5: `client/src/App.tsx`, `client/src/api`, `client/src/pages`.
- Migration `InitialCreate` của PR #5.
- User schema cũ trong PR #5.

Chỉ lấy có chọn lọc:

- Entity/domain KYC.
- Enum KYC.
- Service submit/status/history KYC.
- VNPT real/mock client.
- Private storage service.
- Hash service.
- KYC controller.
- Contract request/response KYC.
- QA docs nếu cần.

## 2. Backend Structure Chuẩn

### 2.1 API Layer

Đặt controller:

```text
server/src/SmartRentalPlatform.Api/Controllers/KycController.cs
```

Controller cần dùng style hiện tại:

- `[ApiController]`
- `[Route("api/kyc")]`
- `[Authorize]` cho endpoint user thật.
- Không tự tạo response format khác hệ thống.
- Dùng `ApiResponse<T>` và exception middleware hiện tại.

Endpoint cần có:

```text
POST /api/kyc/submissions
GET  /api/kyc/my-status
GET  /api/kyc/my-history
```

Dev-only header `X-Dev-User-Id` chỉ dùng khi thật sự cần test local, không thay thế JWT trong flow chuẩn.

### 2.2 Application Layer

Theo cấu trúc hiện tại, interface đặt ở:

```text
server/src/SmartRentalPlatform.Application/Common/Interfaces/
```

Cần thêm:

```text
IKycService.cs
IVnptEkycClient.cs
IPrivateStorageService.cs
IHashService.cs
```

Exception nghiệp vụ đặt ở:

```text
server/src/SmartRentalPlatform.Application/Common/Exceptions/
```

Cần thêm hoặc map vào exception hiện có:

```text
KycBusinessException.cs
```

Không nên tạo namespace mới `Application.Abstractions` như PR #5, vì code hiện tại đang dùng `Application.Common.Interfaces`.

### 2.3 Contracts Layer

Theo style hiện tại, DTO nên đặt theo feature:

```text
server/src/SmartRentalPlatform.Contracts/Kyc/
```

Cần thêm:

```text
SubmitKycRequest.cs
KycSubmissionResponse.cs
KycStatusResponse.cs
KycHistoryItemResponse.cs
KycHistoryResponse.cs
```

Không dùng cấu trúc PR #5:

```text
Contracts/Requests/Kyc
Contracts/Responses/Kyc
```

để giữ đồng bộ với `Contracts/Auth` và `Contracts/Users`.

### 2.4 Domain Layer

Entity hiện tại đã có:

```text
server/src/SmartRentalPlatform.Domain/Entities/Users/KycVerification.cs
```

Cần mở rộng entity này thay vì tạo entity mới ở:

```text
Domain/Entities/KycVerification.cs
```

Các field nên bổ sung từ PR #5:

```text
EkycProvider
EkycSessionId
SelfieCaptureMethod
DocumentCheckResult
FaceMatchScore
FaceMatchResult
LivenessResult
EkycResult
EkycErrorCode
EkycErrorMessage
RiskLevel
```

Giữ các field hiện có:

```text
UserId
DocumentType
FrontImageObjectKey
BackImageObjectKey
SelfieImageObjectKey
OcrFullName
OcrCitizenIdMasked
CitizenIdHash
OcrDateOfBirth
OcrGender
OcrAddress
OcrConfidence
Status
ReviewedByAdminId
RejectedReason
SubmittedAt
ReviewedAt
CreatedAt
UpdatedAt
```

### 2.5 Domain Enums

Code hiện tại đã có:

```text
KycDocumentType
KycVerificationStatus
```

Không tạo trùng `DocumentType` và `KycSubmissionStatus` từ PR #5 nếu không cần.

Nên thêm enum mới:

```text
EkycProvider
SelfieCaptureMethod
DocumentCheckResult
FaceMatchResult
LivenessResult
EkycResult
KycRiskLevel
```

Với status, có 2 lựa chọn:

### Option A: Mở rộng enum hiện tại

```text
KycVerificationStatus
```

Thêm:

```text
PendingEkyc
EkycFailed
PendingAdminReview
Cancelled
```

Ưu điểm: không tạo enum trùng.

### Option B: Dùng enum PR #5 `KycSubmissionStatus`

Không khuyến nghị vì code hiện tại đã có `KycVerificationStatus`.

## 3. Infrastructure Structure Chuẩn

### 3.1 KYC Service

Đặt service:

```text
server/src/SmartRentalPlatform.Infrastructure/Services/Kyc/KycService.cs
```

Service nên inject:

```text
IAppDbContext
IPrivateStorageService
IVnptEkycClient
IHashService
```

Không inject trực tiếp `AppDbContext` nếu muốn giữ clean architecture hiện tại.

### 3.2 VNPT Client

Đặt:

```text
server/src/SmartRentalPlatform.Infrastructure/Ekyc/MockVnptEkycClient.cs
server/src/SmartRentalPlatform.Infrastructure/Ekyc/RealVnptEkycClient.cs
server/src/SmartRentalPlatform.Infrastructure/Ekyc/VnptApiModels.cs
server/src/SmartRentalPlatform.Infrastructure/Options/VnptEkycOptions.cs
```

Config:

```json
"VnptEkyc": {
  "UseMock": true,
  "BaseUrl": "https://api.idg.vnpt.vn",
  "TokenId": "",
  "TokenKey": "",
  "AccessToken": "",
  "AuthMode": "OAuth",
  "MacAddress": "",
  "FaceMatchThreshold": 80.0,
  "TimeoutSeconds": 30
}
```

Secret thật phải dùng user-secrets hoặc environment variable.

### 3.3 Private Storage

Đặt:

```text
server/src/SmartRentalPlatform.Infrastructure/Storage/LocalPrivateStorageService.cs
```

Mặc định lưu disk local:

```text
server/src/SmartRentalPlatform.Api/private-storage/
```

DB chỉ lưu object key, không lưu ảnh binary.

### 3.4 Hash Service

Đặt:

```text
server/src/SmartRentalPlatform.Infrastructure/Security/Sha256HashService.cs
```

Dùng để hash CCCD:

```text
citizen_id_hash
```

Không lưu raw CCCD.

## 4. Persistence / Database

### 4.1 Configuration

Mở rộng file hiện tại:

```text
server/src/SmartRentalPlatform.Infrastructure/Persistence/Configurations/Users/KycVerificationConfiguration.cs
```

Không tạo file ở namespace PR #5:

```text
Persistence/Configurations/KycVerificationConfiguration.cs
```

### 4.2 DbContext

Hiện tại đã có:

```csharp
DbSet<KycVerification> KycVerifications
```

Cần giữ nguyên trong:

```text
AppDbContext
IAppDbContext
```

### 4.3 Migration

Không lấy:

```text
20260523095323_InitialCreate.cs
```

Cần tạo migration mới sau khi chỉnh entity/config:

```powershell
dotnet ef migrations add AddKycVnptFields `
  --project server/src/SmartRentalPlatform.Infrastructure `
  --startup-project server/src/SmartRentalPlatform.Api `
  --output-dir Persistence/Migrations
```

Migration đúng chỉ nên:

- Tạo hoặc alter bảng `kyc_verifications`.
- Thêm cột KYC/VNPT còn thiếu.
- Thêm index cần thiết.
- Không tạo lại bảng `users`, `roles`, `user_roles`.

## 5. Frontend Structure Chuẩn

Không lấy nguyên frontend PR #5:

```text
client/src/App.tsx
client/src/api
client/src/pages
client/src/styles.css
```

Tích hợp vào cấu trúc hiện tại:

```text
client/src/features/kyc/
  pages/
    KycSubmitPage.tsx
    KycStatusPage.tsx
  components/
    WebcamCapture.tsx
    KycUploadForm.tsx
  services/
    kycApi.ts
  types/
    kyc.types.ts
```

API phải đi qua:

```text
client/src/shared/api/apiClient.ts
```

Route thêm vào:

```text
client/src/app/router/routePaths.ts
client/src/app/router/routes.tsx
```

Route gợi ý:

```text
/me/kyc
/me/kyc/status
```

## 6. Integration Order

- [x] Chuẩn hóa domain entity `KycVerification`.
- [x] Bổ sung enum KYC/VNPT không trùng enum hiện tại.
- [x] Cập nhật `KycVerificationConfiguration`.
- [x] Thêm interfaces vào `Application/Common/Interfaces`.
- [x] Thêm DTO vào `Contracts/Kyc`.
- [x] Thêm storage/hash/VNPT client vào Infrastructure.
- [x] Thêm `KycService` dùng `IAppDbContext`.
- [x] Đăng ký DI trong `InfrastructureServiceRegistration`.
- [x] Thêm `KycController` theo response/middleware hiện tại.
- [x] Tạo migration mới, không lấy `InitialCreate`.
- [x] Build backend.
- [x] Tách frontend KYC vào `features/kyc`.
- [x] Build frontend.
- [ ] Test mock KYC.
- [ ] Test real VNPT bằng user-secrets.

## 7. Rủi Ro Cần Tránh

- Không overwrite `Program.cs` hiện tại.
- Không overwrite `client/src/App.tsx` hiện tại.
- Không commit VNPT token/key.
- Không commit ảnh KYC trong `private-storage`.
- Không commit `client/dist`.
- Không lấy migration `InitialCreate` từ PR #5.
- Không tạo enum trùng gây lệch schema.
- Không lưu raw citizen id vào DB.
