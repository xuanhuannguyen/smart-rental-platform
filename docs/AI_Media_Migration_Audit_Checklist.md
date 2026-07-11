# AI Media Migration Audit Checklist

## Mục tiêu

File này là bản `audit checklist sống` cho toàn bộ lộ trình media migration.

Mục đích:

- ghi lại chính xác phase nào đã làm gì
- tách rõ `done / not done / risk / follow-up`
- giảm rủi ro AI hoặc người mới vào dự án hiểu sai trạng thái hiện tại
- làm nguồn sự thật ngắn gọn cho các phase sau

Mỗi phase sau phải cập nhật tiếp vào file này, không tạo lại checklist mới ở nơi khác nếu không thật sự cần.

---

## Quy ước trạng thái

- `[x]` Đã hoàn thành
- `[ ]` Chưa làm
- `[~]` Đã có một phần / chỉ là skeleton / chưa production-ready
- `[!]` Có rủi ro hoặc cần chú ý đặc biệt

---

## Current Status

- `[~]` Phase 0 chưa được materialize thành inventory doc riêng trong `docs`
- `[x]` Phase 1 hoàn thành ở mức schema foundation
- `[x]` Phase 2 hoàn thành ở mức service foundation
- `[x]` Phase 3 hoàn thành ở mức compatibility upload adapter cho flow public upload cũ
- `[x]` Phase 4 hoàn thành cho `ContractFile` main file migration
- `[x]` Phase 5 hoàn thành cho `ContractAppendix` file generation + permission integration
- `[x]` Phase 6 hoàn thành cho `KycVerification` media migration
- `[x]` Phase 7 hoàn thành cho admin private media access + audit wiring
- `[x]` Phase 8 hoàn thành cho `RoomingHouseLegalDocument` media migration
- `[x]` Phase 9 hoàn thành cho backend property image migration
- `[x]` Phase 10 hoàn thành cho frontend public property image migration ở mức consumption/helper
- `[x]` Phase 11 hoàn thành cho backend meter-reading proof media migration
- `[x]` Phase 12 hoàn thành một phần theo hướng safe cleanup cho shared frontend public image helpers
- `[x]` Phase 13 hoàn thành cho avatar media linkage theo hướng compatibility
- `[x]` Runtime code chính hiện bind `IMediaStorageService` và `IPrivateStorageService` sang `S3StorageService`
- `[~]` Open risk hiện tập trung ở private module còn lại, admin access/audit, legal/property/billing migration và cleanup phase sau

---

## Global Invariants

- `[x]` Không đổi endpoint cũ ở Phase 1-2
- `[x]` Không migrate business module cũ ở Phase 1-2
- `[x]` Không xóa fallback field cũ
- `[x]` `MediaAsset` là metadata source of truth cho lộ trình mới
- `[x]` `MediaAuditLog` là audit source cho lộ trình mới
- `[x]` Private media không được coi là có public URL cố định
- `[x]` Database chỉ lưu metadata, không lưu binary

---

## Decision Log

- `[x]` D001: Object key format mới là `public|private/{scope-folder}/{yyyy}/{MM}/{dd}/{guid}{ext}`
- `[x]` D002: Phase 2 chưa thay `IFileStorageService` và `IPrivateStorageService` cũ
- `[x]` D003: `MediaAuditLog.Action` giữ `string` ở giai đoạn đầu
- `[x]` D004: `OwnerUserId` cho phép `null` để hỗ trợ file system-generated
- `[x]` D005: `LinkedEntityType` và `LinkedEntityId` cho phép `null` để hỗ trợ `PendingUpload`
- `[x]` D006: Phase 2 dùng local filesystem làm backend cho `IMediaStorageService`
- `[x]` D007: Private signed URL/local private download thật chưa hoàn chỉnh ở Phase 2
- `[x]` D008: `DefaultMediaPermissionService` chỉ là skeleton, chưa phải permission matrix nghiệp vụ cuối cùng
- `[x]` D009: Compatibility upload ở Phase 3 tiếp tục trả `ObjectKey` và `Url` như contract cũ
- `[x]` D010: Compatibility upload ở Phase 3 map toàn bộ `FilesController` upload hiện tại sang `MediaVisibility.Public`
- `[x]` D011: `ContractFile` main file dùng `MediaAssetId` nhưng vẫn giữ `StorageObjectKey` làm fallback legacy
- `[x]` D012: `ContractFileResponse` được expose thêm `MediaAssetId` nhưng chưa bỏ field cũ
- `[x]` D013: `DefaultMediaPermissionService` đã hiểu `ContractFile` main file private access, nhưng chưa phủ `ContractAppendix`
- `[x]` D014: `ContractAppendixService` tạo appendix PDF mới qua media core và vẫn giữ `StorageObjectKey` làm fallback row-level
- `[x]` D015: `DefaultMediaPermissionService` đã phủ `ContractAppendix` raw/masked access theo cùng business rule với `ContractFileService`
- `[x]` D016: Runtime storage hiện dùng `S3StorageService`; `LocalMediaStorageService` không còn là binding chính trong app runtime
- `[x]` D017: `KycService` đã đi qua media core nhưng VNPT client vẫn nhận `objectKey` như trước để giữ nguyên provider integration
- `[x]` D018: `AdminKycDetailResponse` và `AdminKycInfo` đã expose `Front/Back/SelfieMediaAssetId`, nhưng URL admin vẫn còn object-key based cho đến Phase 7
- `[x]` D019: `MeterReading` proof image dùng `ProofMediaAssetId` nhưng vẫn giữ `ProofImageObjectKey` làm fallback/compatibility
- `[x]` D020: `DefaultMediaPermissionService` cho meter-reading proof cho phép landlord luôn xem; tenant/occupant chỉ xem khi invoice không còn `Draft`
- `[x]` D021: Phase 12 chỉ tách helper frontend cho public listing/property image; không động vào helper private/transitional chưa chốt
- `[x]` D022: `User` avatar dùng thêm `AvatarMediaAssetId` nhưng vẫn giữ `AvatarUrl` để compatibility
- `[x]` D023: Google/external avatar tiếp tục đi qua `AvatarUrl`, không ép phải có `MediaAssetId`
- `[x]` D024: Phase 13 chỉ làm `avatar`, không gộp `house rule PDF` vào cùng phase mặc định

---

## Artifact Inventory

- `[x]` Có phase plan tổng tại `docs/AI_Media_Migration_Phase_Plan.md`
- `[x]` Có phase-specific plan cho contract file tại `docs/AI_Media_Migration_Phase4_ContractFile_Plan.md`
- `[x]` Có phase-specific plan cho avatar tại `docs/AI_Media_Migration_Phase13_Avatar_Plan.md`
- `[x]` Có context guardrail tại `docs/AI_Context_Continuity_Guardrails.md`
- `[x]` Có audit checklist sống tại `docs/AI_Media_Migration_Audit_Checklist.md`
- `[~]` Chưa có inventory doc riêng cho Phase 0 được materialize thành artifact độc lập
- `[x]` Phase sau phải cập nhật tiếp ngay trong file audit này, không tạo file audit mới trừ khi có lý do mạnh

---

## Phase 0 Audit

### Mục tiêu phase

- `[~]` Chốt baseline và inventory để hỗ trợ migration nhiều phase

### Trạng thái thực tế theo code/docs hiện tại

- `[x]` Đã có phase plan tổng
- `[x]` Đã có context guardrail
- `[x]` Đã có audit checklist sống
- `[~]` Một phần inventory đang nằm rải trong phase plan và audit, chưa tách thành inventory doc riêng

### Chưa materialize thành artifact riêng

- `[ ]` Chưa có file inventory module/file-field-endpoint riêng
- `[ ]` Chưa có checklist migration target độc lập dạng bảng

### Kết luận phase

- `[~]` Phase 0 được cover một phần ở mức planning, chưa hoàn thành đầy đủ theo deliverable gốc

---

## Phase 1 Audit

### Mục tiêu phase

- `[x]` Dựng `media core schema` độc lập với business flow cũ

### Schema và domain

- `[x]` Thêm `MediaScope`
- `[x]` Thêm `MediaVisibility`
- `[x]` Thêm `MediaStatus`
- `[x]` Thêm entity `MediaAsset`
- `[x]` Thêm entity `MediaAuditLog`

### DbContext và EF

- `[x]` Thêm `DbSet<MediaAsset>` vào `IAppDbContext`
- `[x]` Thêm `DbSet<MediaAuditLog>` vào `IAppDbContext`
- `[x]` Thêm `DbSet<MediaAsset>` vào `AppDbContext`
- `[x]` Thêm `DbSet<MediaAuditLog>` vào `AppDbContext`
- `[x]` Thêm `MediaAssetConfiguration`
- `[x]` Thêm `MediaAuditLogConfiguration`

### Migration

- `[x]` Tạo migration `AddMediaSchemaFoundation`
- `[x]` Migration chỉ tạo `media_assets`
- `[x]` Migration chỉ tạo `media_audit_logs`
- `[x]` Có unique index cho `media_assets.object_key`
- `[x]` Có FK `media_audit_logs.media_asset_id -> media_assets.id`
- `[x]` Snapshot đã cập nhật đúng

### Xác nhận không đổi ngoài scope

- `[x]` Không sửa `FilesController`
- `[x]` Không sửa `KycController`
- `[x]` Không sửa `AdminMediaController`
- `[x]` Không sửa `LocalFileStorageService`
- `[x]` Không sửa `LocalPrivateStorageService`
- `[x]` Không thêm `MediaAssetId` vào entity business cũ
- `[x]` Không xóa field legacy cũ

### Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj` pass

### Ghi chú / follow-up

- `[x]` Phase 1 chỉ là schema, chưa có service layer
- `[x]` Migration phải generate ngoài sandbox do `NuGet.Config` bị chặn

---

## Phase 2 Audit

### Mục tiêu phase

- `[x]` Dựng media service foundation

### Interfaces mới

- `[x]` Thêm `IMediaStorageService`
- `[x]` Thêm `IMediaAssetService`
- `[x]` Thêm `IMediaAccessService`
- `[x]` Thêm `IMediaPermissionService`
- `[x]` Thêm `IMediaObjectKeyFactory`

### Models mới

- `[x]` Thêm `MediaUploadRequest`
- `[x]` Thêm `MediaStoredObjectResult`
- `[x]` Thêm `CreateMediaAssetRequest`
- `[x]` Thêm `MediaAccessResult`
- `[x]` Thêm `MediaObjectKeyResult`

### Infrastructure implementation

- `[x]` Thêm `MediaObjectKeyFactory`
- `[x]` Thêm `LocalMediaStorageService`
- `[x]` Thêm `MediaAssetService`
- `[x]` Thêm `MediaAccessService`
- `[x]` Thêm `DefaultMediaPermissionService`
- `[x]` Đăng ký DI cho media foundation

### Kiến trúc / behavior đã có

- `[x]` Có object key strategy thống nhất
- `[x]` Có metadata create flow cho `MediaAsset`
- `[x]` Có soft-delete metadata skeleton (`MarkDeletedAsync`)
- `[x]` Có access skeleton cho open-read / get-download-url
- `[x]` Có audit log skeleton trong `MediaAccessService`

### Kiến trúc / behavior chưa hoàn chỉnh

- `[~]` Runtime private flow hiện chủ yếu vẫn đọc qua `OpenReadAsync`, chưa chuyển hẳn sang signed download URL ở application flow
- `[~]` Permission matrix mới chỉ là skeleton owner-based
- `[~]` Audit logging mới ở mức foundation, chưa có IP/UserAgent/Reason flow thật
- `[~]` `LocalMediaStorageService` vẫn tồn tại như implementation phụ/test foundation, nhưng không còn là binding runtime chính

### Xác nhận không đổi ngoài scope

- `[x]` Không thay `IFileStorageService`
- `[x]` Không thay `IPrivateStorageService`
- `[x]` Không sửa endpoint cũ
- `[x]` Không migrate business service cũ sang media layer mới

### Tests

- `[x]` Có test object key generation
- `[x]` Có test metadata create/delete
- `[x]` Có test local media storage upload/open/delete
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --filter Media` pass

### Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj` pass

### Findings / risk mở

- `[!]` Dù storage layer đã có signed URL cho S3, private application flow hiện tại vẫn chưa dùng đó làm đường chính
- `[!]` `DefaultMediaPermissionService` chưa phù hợp cho contract/KYC/legal/admin access thật
- `[!]` `MediaAccessService` đang ghi audit ngay trong access flow, phase sau cần chốt rõ audit failure có được phép làm fail read/download hay không

### Deferred intentionally

- `[x]` Ở thời điểm kết thúc Phase 2, private signed URL end-to-end chưa được đưa vào application flow vì phase này chỉ dựng foundation
- `[x]` Chưa làm permission matrix đầy đủ vì Phase 2 chỉ dựng media service skeleton, chưa migrate module nghiệp vụ
- `[x]` Ở thời điểm kết thúc Phase 2, chưa cắm S3-backed implementation vào code chính vì đang ưu tiên schema/service foundation với diff nhỏ
- `[x]` Chưa đụng controller/business flow cũ vì phải giữ compatibility cho Phase 3 trở đi

---

## Phase 3 Audit

### Mục tiêu phase

- `[x]` Giữ endpoint upload cũ hoạt động nhưng bên dưới đã dùng media core

### Thay đổi implementation thực tế

- `[x]` Thêm `MediaBackedFileStorageService`
- `[x]` Giữ nguyên `FilesController` contract cũ
- `[x]` Đổi DI của `IFileStorageService` sang adapter mới
- `[x]` Upload cũ bây giờ đi qua `IMediaObjectKeyFactory`
- `[x]` Upload cũ bây giờ đi qua `IMediaStorageService`
- `[x]` Upload cũ bây giờ tạo `MediaAsset` metadata qua `IMediaAssetService`

### Compatibility giữ nguyên

- `[x]` Vẫn trả `FileUploadResponse.ObjectKey`
- `[x]` Vẫn trả `FileUploadResponse.Url`
- `[x]` Không đổi endpoint `api/files/images`
- `[x]` Không đổi endpoint `api/files/pdfs`
- `[x]` Không đổi validation hiện tại trong `FilesController`

### Mapping hiện tại theo code

- `[x]` `RoomingHouse -> MediaScope.RoomingHouseImage`
- `[x]` `Room -> MediaScope.RoomImage`
- `[x]` `LegalDocument -> MediaScope.RoomingHouseLegalDocument`
- `[x]` `Avatar -> MediaScope.Avatar`
- `[x]` `HouseRule -> MediaScope.RoomingHouseRulePdf`
- `[x]` Tất cả compatibility upload hiện tại được map sang `MediaVisibility.Public`

### Xác nhận không đổi ngoài scope

- `[x]` Không sửa `KycController`
- `[x]` Không sửa `KycService`
- `[x]` Không sửa `ContractFileService`
- `[x]` Không thay `IPrivateStorageService`
- `[x]` Chưa migrate bất kỳ business entity nào sang `MediaAssetId`

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --filter MediaBackedFileStorageServiceTests` pass
- `[x]` Có test tạo `MediaAsset` từ upload compatibility flow
- `[x]` Có test `HouseRule` PDF map đúng scope và giữ đúng `FileSize`

### Findings / risk mở

- `[!]` Public upload compatibility hiện dùng object key format mới `public/...`, phase sau không được giả định object key còn giữ folder naming legacy cũ
- `[!]` Business module cũ vẫn chỉ lưu `ObjectKey/ImageUrl`, chưa link ngược sang `MediaAssetId`
- `[!]` Legal document upload vẫn đang là public compatibility flow vì Phase 3 ưu tiên giữ hành vi cũ, chưa phải final private-media design

### Deferred intentionally

- `[x]` Chưa đổi contract API upload để thêm `MediaAssetId` vì phải giữ compatibility cho frontend/module cũ
- `[x]` Chưa đổi `FilesController` sang response mới vì Phase 3 chỉ là adapter foundation
- `[x]` Chưa migrate module business nào đọc `MediaAssetId` vì phase kế tiếp sẽ tách từng module

---

## Phase 4 Audit

### Mục tiêu phase

- `[x]` Chuyển `ContractFile` main file sang `MediaAssetId` với thay đổi nghiệp vụ tối thiểu

### Schema / persistence

- `[x]` Thêm `ContractFile.MediaAssetId`
- `[x]` Thêm navigation `ContractFile -> MediaAsset`
- `[x]` Thêm EF config cột/index/FK cho `media_asset_id`
- `[x]` Tạo migration `AddContractFileMediaAssetLink`
- `[x]` Migration có backfill `ContractFile` main file cũ sang `media_assets`

### Service integration

- `[x]` `GenerateSignedContractFileAsync` tạo private media qua media layer
- `[x]` `GenerateSignedContractFileAsync` tạo `MediaAsset` linked với `ContractFile`
- `[x]` `StorageObjectKey` vẫn được giữ cho row mới như fallback/compatibility
- `[x]` `OpenFileAsync` ưu tiên `IMediaAccessService` nếu có `MediaAssetId`
- `[x]` `OpenFileAsync` fallback về `IPrivateStorageService` cho dữ liệu legacy
- `[x]` `ContractFileResponse` expose `MediaAssetId`

### Permission / access

- `[x]` `DefaultMediaPermissionService` đã hỗ trợ contract main file private access
- `[x]` Raw contract file vẫn chỉ cho landlord/main-tenant lineage
- `[x]` Masked contract file vẫn cho occupant hợp lệ
- `[x]` Không đổi business rule generate raw/masked hiện tại

### Xác nhận không đổi ngoài scope

- `[x]` Không migrate `ContractAppendixService`
- `[x]` Không sửa `FilesController`
- `[x]` Không sửa `KycService`
- `[x]` Không thay `IPrivateStorageService`
- `[x]` Không bỏ `StorageObjectKey`
- `[x]` Không bỏ `FileUrl`

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --filter "ContractFileServiceTests|DefaultMediaPermissionServiceTests"` pass
- `[x]` Có test generate contract file mới tạo `MediaAssetId`
- `[x]` Có test open file mới đi qua media layer
- `[x]` Có test open file cũ fallback qua `IPrivateStorageService`
- `[x]` Có test permission raw/masked cho contract media private

### Findings / risk mở

- `[!]` `DefaultMediaPermissionService` mới chỉ cover `ContractFile` main file, chưa cover `ContractAppendix`
- `[!]` Legacy backfill metadata cho `ContractFile` cũ đang dùng `file_size = 0` vì migration không đọc được binary metadata từ storage
- `[!]` `GetDownloadUrlAsync` cho private media vẫn chưa là đường chính ở phase này; `OpenReadAsync` mới là path đã được dùng thật

### Deferred intentionally

- `[x]` Chưa migrate appendix file vì để riêng cho Phase 5
- `[x]` Chưa bỏ `StorageObjectKey` vì cần fallback cho dữ liệu cũ
- `[x]` Chưa bỏ `FileUrl` dù contract private hiện chủ yếu là `null`
- `[x]` Chưa làm admin/audit semantics chi tiết cho contract media access

---

## Phase 5 Audit

### Mục tiêu phase

- `[x]` Chuyển `ContractAppendix` file sang `MediaAssetId` với thay đổi nghiệp vụ tối thiểu

### Service integration

- `[x]` `ContractAppendixService` generate appendix PDF mới qua `IMediaStorageService`
- `[x]` `ContractAppendixService` tạo `MediaAsset` private linked với `ContractFile`
- `[x]` Appendix `ContractFile` mới vẫn giữ `StorageObjectKey` làm fallback/compatibility
- `[x]` `ContractAppendixResponse` tiếp tục expose `MediaAssetId` từ `ContractFileResponse`

### Permission / access

- `[x]` `DefaultMediaPermissionService` đã hỗ trợ private access cho appendix file
- `[x]` Raw appendix file vẫn cho landlord, current main tenant, previous main tenant và user được đổi thành main tenant
- `[x]` Masked appendix file vẫn chỉ cho occupant hợp lệ ngoài nhóm được xem raw
- `[x]` Không nới lỏng business rule appendix hiện tại

### Xác nhận không đổi ngoài scope

- `[x]` Không sửa `FilesController`
- `[x]` Không sửa `KycService`
- `[x]` Không sửa flow ký contract chính
- `[x]` Không bỏ `StorageObjectKey`
- `[x]` Không thêm migration/backfill cho appendix legacy trong phase này

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj --no-restore` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-restore --filter "FullyQualifiedName~ContractAppendixServiceTests|FullyQualifiedName~DefaultMediaPermissionServiceTests|FullyQualifiedName~ContractFileServiceTests"` pass
- `[x]` Có test generate appendix file mới tạo `MediaAssetId` cho raw/masked variants
- `[x]` Có test permission raw/masked cho appendix private access

### Findings / risk mở

- `[!]` Appendix `ContractFile` legacy cũ vẫn có thể chưa có `MediaAssetId`; phase sau phải quyết định có backfill hay giữ fallback dài hạn
- `[!]` `DefaultMediaPermissionService` hiện đã cover contract main + appendix, nhưng chưa phải permission matrix hoàn chỉnh cho KYC/legal/admin/private module khác
- `[!]` Đường private access đang dùng thật vẫn là `OpenReadAsync`, chưa phải signed download URL

### Deferred intentionally

- `[x]` Chưa thêm migration/backfill appendix legacy vì phase này ưu tiên diff nhỏ và giữ tách biệt với schema/data migration mới
- `[x]` Chưa làm audit view/download chi tiết cho appendix vì semantics audit chung còn để Phase 7 chốt
- `[x]` Chưa đụng module private khác để tránh mở rộng scope

---

## Phase 6 Audit

### Mục tiêu phase

- `[x]` Chuyển 3 file KYC sang `MediaAssetId`, giữ nguyên logic eKYC

### Schema / persistence

- `[x]` Thêm `KycVerification.FrontMediaAssetId`
- `[x]` Thêm `KycVerification.BackMediaAssetId`
- `[x]` Thêm `KycVerification.SelfieMediaAssetId`
- `[x]` Thêm EF config cột/index/FK cho 3 media asset link
- `[x]` Tạo migration `AddKycVerificationMediaAssets`
- `[x]` Migration có backfill KYC legacy sang `media_assets`

### Service integration

- `[x]` `KycService` upload file qua `IMediaStorageService`
- `[x]` `KycService` tạo `MediaAsset` private linked với `KycVerification`
- `[x]` `KycService` vẫn giữ `FrontImageObjectKey`, `BackImageObjectKey`, `SelfieImageObjectKey`
- `[x]` VNPT client vẫn nhận `objectKey` như flow cũ
- `[x]` Submit fail sau upload có cleanup object mới upload

### Admin/read model preparation

- `[x]` `AdminKycDetailResponse` expose `FrontMediaAssetId`
- `[x]` `AdminKycDetailResponse` expose `BackMediaAssetId`
- `[x]` `AdminKycDetailResponse` expose `SelfieMediaAssetId`
- `[x]` `AdminKycInfo` expose `FrontMediaAssetId`
- `[x]` `AdminKycInfo` expose `BackMediaAssetId`
- `[x]` `AdminKycInfo` expose `SelfieMediaAssetId`
- `[x]` URL admin hiện vẫn fallback theo `objectKey`

### Xác nhận không đổi ngoài scope

- `[x]` Không đổi `KycController` contract hiện tại
- `[x]` Không đổi risk calculation
- `[x]` Không đổi duplicate approved KYC rule
- `[x]` Không đổi duplicate citizen ID rule
- `[x]` Không đổi approval workflow
- `[x]` Không bỏ 3 field `FrontImageObjectKey`, `BackImageObjectKey`, `SelfieImageObjectKey`

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj --no-restore` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-restore --filter "FullyQualifiedName~KycServiceTests|FullyQualifiedName~AdminKycApprovalServiceTests"` pass
- `[x]` Có test submit KYC mới tạo đủ 3 `MediaAsset`
- `[x]` Có test cleanup object khi submit fail vì duplicate citizen ID
- `[x]` Có test admin KYC detail/history map ra `MediaAssetId`

### Findings / risk mở

- `[!]` Legacy KYC backfill đang dùng `file_size = 0` và content-type suy luận từ extension

### Deferred intentionally

- `[x]` Chưa đổi `AdminMediaController` sang media-asset based access vì để riêng cho Phase 7
- `[x]` Chưa audit view/download cho admin KYC vì semantics audit chung chưa chốt
- `[x]` Chưa bỏ 3 field object key cũ vì vẫn cần fallback và provider compatibility

---

## Phase 7 Audit

### Mục tiêu phase

- `[x]` Chuẩn hóa admin private media access qua media core
- `[x]` Tách hành vi `view` và `download`
- `[x]` Ghi audit thật cho admin media access

### Đã làm

- `[x]` `AdminMediaController` có endpoint `GET /api/admin/media/private/{mediaAssetId}`
- `[x]` `AdminMediaController` có endpoint `GET /api/admin/media/private/{mediaAssetId}/download`
- `[x]` `AdminKycDetailResponse` build URL theo `mediaAssetId`
- `[x]` `AdminKycInfo` build URL theo `mediaAssetId`
- `[x]` `MediaAccessService.OpenReadAsync` nhận `MediaAuditContext`
- `[x]` Audit log ghi được `Action`
- `[x]` Audit log ghi được `IpAddress`
- `[x]` Audit log ghi được `UserAgent`
- `[x]` Audit log ghi được `Reason`
- `[x]` Audit log ghi được `MetadataJson`
- `[x]` `DefaultMediaPermissionService` cho phép admin truy cập private media
- `[x]` Có test verify admin được xem private KYC media
- `[x]` Có test verify audit log được tạo với metadata

### Chưa hoàn chỉnh

- `[~]` Legacy endpoint `GET /api/admin/media/private?objectKey=...` vẫn còn giữ để không làm gãy legal document flow trước Phase 8
- `[~]` Legal document admin viewer vẫn chưa đi qua `mediaAssetId` vì entity đó chưa migrate
- `[~]` Chưa dùng signed URL/admin token ngắn hạn; hiện admin download vẫn stream qua backend

### Không làm trong phase này

- `[x]` Không migrate `RoomingHouseLegalDocument`
- `[x]` Không đổi contract user-facing download flow
- `[x]` Không bỏ các field object key cũ ở KYC

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj --no-restore` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MediaAccessServiceTests|FullyQualifiedName~DefaultMediaPermissionServiceTests|FullyQualifiedName~AdminKycApprovalServiceTests|FullyQualifiedName~AdminUserServiceTests|FullyQualifiedName~ContractFileServiceTests"` pass

### Findings / risk mở

- `[!]` Legacy admin object-key endpoint vẫn còn tồn tại cho module legal chưa migrate, nên object-key based access chưa thể xóa hẳn ở Phase 7
- `[!]` Permission matrix cho legal document và meter reading proof vẫn chưa hoàn thiện
- `[!]` Chưa có integration test end-to-end gọi thật `AdminMediaController` với bucket thật

---

## Chưa làm sau Phase 1-7

### Compatibility và migration

- `[x]` Đã migrate `RoomingHouseLegalDocument`
- `[ ]` Chưa migrate `PropertyImage`
- `[x]` Đã migrate `MeterReading`

### Access và permission thật

- `[ ]` Chưa có permission matrix đúng nghiệp vụ cho contract
- `[x]` Có permission cơ bản đúng cho legal document:
  - landlord sở hữu khu trọ được xem
  - admin được xem
  - tenant/guest không được xem
- `[x]` Có permission cơ bản đúng cho meter reading proof:
  - landlord được xem
  - tenant/occupant chỉ xem khi invoice không còn `Draft`
- `[x]` Admin private viewer cho media-backed KYC đã đi qua `MediaAssetId`
- `[x]` Admin private viewer cho legal document đã đi qua `MediaAssetId`

### Audit thật

- `[x]` Có ghi `IpAddress`
- `[x]` Có ghi `UserAgent`
- `[x]` Có ghi `Reason`
- `[x]` Có audit `View`/`Download` ở admin flow media-backed
- `[x]` Legal document media-backed route `/api/media/private/{mediaAssetId}` đã đi qua media-core audit
- `[~]` Legacy object-key admin route vẫn chưa ghi audit media-core nếu còn được gọi trực tiếp

---

## Phase 8 Audit

### Mục tiêu phase

- `[x]` Chuyển `RoomingHouseLegalDocument` sang media core
- `[x]` Giữ compatibility cho legal document flow hiện tại
- `[x]` Cho admin và landlord xem legal document qua private media access chung

### Đã làm

- `[x]` Thêm `FrontMediaAssetId`, `BackMediaAssetId`, `ExtraMediaAssetId` vào `RoomingHouseLegalDocument`
- `[x]` Thêm EF config/index/FK cho 3 media asset link
- `[x]` Tạo migration `AddRoomingHouseLegalDocumentMediaAssets`
- `[x]` Migration có backfill legal doc legacy sang `media_assets`
- `[x]` `RoomingHouseMediaService` link legal doc object key sang `MediaAsset`
- `[x]` Legal document response expose `Front/Back/ExtraMediaAssetId`
- `[x]` Legal document response expose `Front/Back/ExtraImageUrl`
- `[x]` `AdminRoomingHouseApprovalService` map legal doc private URL qua `mediaAssetId`
- `[x]` `MediaController` có authenticated private route cho media-backed legal doc
- `[x]` `DefaultMediaPermissionService` cover landlord/admin access cho legal document private media
- `[x]` Upload `FileUploadScope.LegalDocument` mới dùng `MediaVisibility.Private`

### Chưa hoàn chỉnh

- `[~]` Một số legal document cũ có thể đang nằm ở object key public cũ; metadata đã chuyển sang private semantics nhưng object vật lý chưa được move/rewrite
- `[~]` Legacy object-key fields vẫn còn cần giữ cho compatibility và submit validation
- `[~]` Chưa có migration dọn object public cũ hoặc rotate object key cũ

### Không làm trong phase này

- `[x]` Không migrate `PropertyImage`
- `[x]` Không migrate `MeterReading`
- `[x]` Không bỏ hẳn legacy admin object-key endpoint

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj --no-restore` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-restore --filter "FullyQualifiedName~RoomingHouseMediaServiceTests|FullyQualifiedName~AdminRoomingHouseApprovalServiceTests|FullyQualifiedName~DefaultMediaPermissionServiceTests|FullyQualifiedName~MediaAccessServiceTests"` pass

### Findings / risk mở

- `[!]` Legal document object đã upload trước Phase 8 có thể vẫn nằm ở `public/...` object key cũ, nên cần planned cleanup nếu muốn chặn hoàn toàn public reachability ở tầng object-store
- `[!]` `RoomingHouseSubmissionService` và các rule hiện vẫn validate theo object key legacy fields
- `[!]` Chưa có integration test gọi thật upload legal-doc -> save legal-doc -> open private route với bucket thật

### Cleanup

- `[ ]` Chưa có backfill dữ liệu cũ
- `[ ]` Chưa bỏ fallback field cũ
- `[ ]` Chưa xóa hoàn toàn object-key based access cũ

---

## Phase 9 Audit

### Mục tiêu phase

- `[x]` Chuyển backend `PropertyImage` sang media core
- `[x]` Giữ compatibility cho frontend public image flow hiện tại
- `[x]` Backfill property image legacy sang `media_assets`

### Đã làm

- `[x]` Thêm `PropertyImage.MediaAssetId`
- `[x]` Thêm EF config/index/FK cho `PropertyImage.MediaAssetId`
- `[x]` Tạo migration `AddPropertyImageMediaAssets`
- `[x]` Migration có backfill property image legacy sang `media_assets`
- `[x]` `RoomingHouseMediaService.UpdateImagesAsync` đã link ảnh khu trọ sang `MediaAsset`
- `[x]` `RoomMediaService.UpdateImagesAsync` đã link ảnh phòng sang `MediaAsset`
- `[x]` `PropertyImageResponse` expose `MediaAssetId`
- `[x]` `RoomingHouseReadModelMapper` map `MediaAssetId` cho ảnh public
- `[x]` `RoomReadModelMapper` map `MediaAssetId` cho ảnh public
- `[x]` `AdminRoomingHouseApprovalService` map `MediaAssetId` cho ảnh public

### Chưa hoàn chỉnh

- `[~]` Public route hiện vẫn đọc qua `/api/media/public/{objectKey}`, chưa chuyển sang media-backed public resolver
- `[~]` Frontend vẫn còn có thể phụ thuộc vào `ObjectKey/ImageUrl` ở một số chỗ cho đến Phase 10
- `[~]` Legacy field `ObjectKey` và `ImageUrl` vẫn phải giữ để compatibility

### Không làm trong phase này

- `[x]` Không migrate frontend public image flow
- `[x]` Không bỏ `ObjectKey`
- `[x]` Không thêm permission gate mới cho public route

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj --no-restore` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-restore --filter "FullyQualifiedName~RoomingHouseMediaServiceTests|FullyQualifiedName~RoomMediaServiceTests|FullyQualifiedName~AdminRoomingHouseApprovalServiceTests|FullyQualifiedName~MediaBackedFileStorageServiceTests"` pass

### Findings / risk mở

- `[!]` Public object access runtime hiện vẫn object-key based; `MediaAssetId` mới đang là metadata source of truth ở backend write/read-model layer, chưa phải fetch layer cuối
- `[!]` Rooming house/room image cũ được backfill metadata nhưng không đổi object key vật lý
- `[!]` Phase 10 phải dọn frontend helper để tránh tiếp tục coi `objectKey` là API contract chính

---

## Phase 10 Audit

### Mục tiêu phase

- `[x]` Dọn frontend public image flow để ưu tiên contract mới từ backend
- `[x]` Giảm phụ thuộc `toAssetUrl(objectKey)` ở property image UI
- `[x]` Giữ compatibility cho các module chưa migrate hết

### Đã làm

- `[x]` Tách helper `toPublicAssetUrl(imageUrl, objectKey)` cho public property image
- `[x]` `HouseImageGallery` dùng helper public riêng
- `[x]` `PropertyImageEditor` dùng helper public riêng
- `[x]` `PublicRoomingHouseDetailPage` dùng helper public riêng cho room image
- `[x]` Client type `PropertyImage` expose `mediaAssetId`
- `[x]` Client type `PropertyImageRequest` giữ được `mediaAssetId` trong local state
- `[x]` Upload response client type đã hiểu `mediaAssetId`

### Chưa hoàn chỉnh

- `[~]` `toAssetUrl` generic vẫn còn cần cho avatar/legal/pdf/private compatibility flows
- `[~]` Các listing/card dùng `coverImageUrl` vẫn đi qua helper generic, nhưng backend field này đã là URL-ready nên chưa phải blocker
- `[~]` Frontend chưa dùng `MediaAssetId` để fetch public object trực tiếp; phase này chỉ dọn consumption/helper

### Không làm trong phase này

- `[x]` Không refactor layout/UI
- `[x]` Không đổi legal document/private image viewer
- `[x]` Không thay public fetch API ở backend

### Tests / Verify

- `[x]` `rg` không còn pattern `image.imageUrl || image.objectKey` ở các component property image chính sau khi migrate
- `[x]` Diff Phase 10 tập trung ở helper/types/components public image
- `[~]` `npm.cmd run build` bị chặn bởi lỗi sẵn có ở `react-pdf` preview modules, không phải do Phase 10

### Findings / risk mở

- `[!]` Frontend build hiện đang fail ở `ContractPreviewModal.tsx` và `AppendixPreviewModal.tsx` vì `react-pdf`, nên chưa có full frontend build green cho repo tại thời điểm chốt Phase 10
- `[!]` `toAssetUrl` generic vẫn tồn tại nên phase sau cần tiếp tục thu hẹp nơi sử dụng nếu muốn cleanup sâu hơn

---

## Phase 11 Audit

### Mục tiêu phase

- `[x]` Chuyển `MeterReading` proof image sang media core
- `[x]` Giữ compatibility cho `ProofImageObjectKey` cũ
- `[x]` Thêm private access rule cho landlord/tenant/occupant theo invoice status

### Đã làm

- `[x]` Thêm `MeterReading.ProofMediaAssetId`
- `[x]` Thêm navigation `MeterReading -> ProofMediaAsset`
- `[x]` Thêm EF config/index/FK cho `proof_media_asset_id`
- `[x]` Tạo migration `AddMeterReadingProofMediaAssets`
- `[x]` Migration có backfill meter reading legacy từ `ProofImageObjectKey` sang `media_assets`
- `[x]` `BillingService` link proof image sang `MediaAsset` khi tạo invoice + meter reading mới
- `[x]` `LatestMeterReadingResponse` expose `ProofMediaAssetId`
- `[x]` `LatestMeterReadingResponse` expose `ProofImageUrl`
- `[x]` `InvoiceItemResponse` expose `MeterReadingProofMediaAssetId`
- `[x]` `InvoiceItemResponse` expose `MeterReadingProofImageUrl`
- `[x]` `DefaultMediaPermissionService` đã hỗ trợ private access cho meter-reading proof
- `[x]` Thêm upload scope `MeterReading` vào compatibility upload layer và map sang private media
- `[x]` Rule private access hiện hành được implement theo hướng:
  - landlord luôn xem được
  - tenant/occupant chỉ xem khi invoice không còn `Draft`

### Chưa hoàn chỉnh

- `[~]` Frontend mới chỉ cập nhật typing; chưa có uploader/viewer proof image hoàn chỉnh ở landlord billing UI hoặc tenant invoice UI
- `[~]` `ProofImageObjectKey` vẫn còn cần giữ để compatibility và fallback
- `[~]` Chưa có rule chỉnh sửa meter proof sau khi invoice đã `Paid` vì hiện chưa có flow update proof image riêng

### Không làm trong phase này

- `[x]` Không xóa `ProofImageObjectKey`
- `[x]` Không refactor toàn bộ landlord billing UI
- `[x]` Không thêm endpoint update meter proof riêng sau khi invoice đã tạo

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj --no-restore` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-restore --filter "FullyQualifiedName~BillingServiceTests|FullyQualifiedName~DefaultMediaPermissionServiceTests"` pass
- `[x]` Có test generate invoice mới link proof image vào `MediaAsset`
- `[x]` Có test tenant/occupant bị chặn khi invoice còn `Draft`
- `[x]` Có test tenant/occupant được xem proof image ở trạng thái published mẫu là `Issued`
- `[x]` Chưa có test update-block sau `Paid` vì code hiện chưa có flow update proof image riêng

### Findings / risk mở

- `[!]` UI cho meter proof hiện chưa materialize, nên Phase 11 mới hoàn chỉnh ở backend/contract layer
- `[!]` Backfill meter-reading proof dùng metadata suy luận từ object key cũ, chưa có file-size thật cho legacy object
- `[!]` Nếu phase sau thêm flow chỉnh sửa proof image sau khi invoice được tạo, cần chốt rõ rule `Paid` và audit semantics trước khi code
- `[!]` Một số đoạn phase-plan cũ dễ khiến hiểu nhầm là rule `Paid` đã được enforce; implementation hiện tại mới enforce rule xem theo `Draft` vs published

---

## Phase 12 Audit

### Mục tiêu phase

- `[x]` Dọn shared frontend asset helpers theo phạm vi an toàn
- `[x]` Tách rõ helper public display ra khỏi helper generic/transitional
- `[x]` Tránh đụng vào các module private hoặc business flow còn chưa chốt

### Đã làm

- `[x]` Thêm `toPublicListingImageUrl` trong `client/src/shared/api/assets.ts`
- `[x]` Thêm `toPublicPropertyImageUrl` trong `client/src/shared/api/assets.ts`
- `[x]` Giữ `toAssetUrl` làm compatibility helper cho flow legacy/transitional
- `[x]` Đổi các call site public rõ ràng sang helper mới:
  - `SearchRoomingHousesPage`
  - `PublicRoomingHouseDetailPage`
  - `HouseImageGallery`
  - `MePage` phần thẻ listing public
  - `LandlordDashboardPage` phần cover image
  - `RentalAiChatbot` phần mini rooming-house card

### Chưa hoàn chỉnh

- `[~]` Chưa tách helper private fetch/open riêng thành abstraction cuối cùng
- `[~]` `toAssetUrl` vẫn còn được dùng ở avatar và nhiều flow transitional khác
- `[~]` Chưa dọn `houseRule` PDF, legal document, KYC, admin private image rendering

### Không làm trong phase này

- `[x]` Không refactor avatar/profile image flow
- `[x]` Không đổi private download/open semantics
- `[x]` Không chỉnh các module business chưa hoàn thiện như chat attachment hoặc private proof viewer UI

### Tests / Verify

- `[~]` Đã chạy `npm run build` trong `client`, nhưng build tổng hiện đang fail bởi lỗi TypeScript có sẵn ở `ContractPreviewModal` và `AppendixPreviewModal` liên quan `react-pdf`, không nằm trong diff Phase 12
- `[x]` Các helper mới và call site public đã đổi không phát sinh lỗi riêng được thấy từ lần verify này

### Findings / risk mở

- `[!]` Naming ở frontend đã rõ hơn, nhưng source data vẫn còn mixed giữa `imageUrl` và `objectKey` nên chưa thể bỏ hoàn toàn compatibility helper
- `[!]` Nếu Phase 13+ động vào avatar hoặc private viewer, cần tiếp tục tách helper theo semantics thay vì mở rộng lại `toAssetUrl`
- `[!]` Frontend build tổng hiện chưa dùng được như gate mạnh cho Phase 12 vì đang bị chặn bởi lỗi unrelated ở preview PDF module

---

## Phase 13 Audit

### Mục tiêu phase

- `[x]` Nối `User` avatar vào media core theo hướng compatibility
- `[x]` Giữ `AvatarUrl` để không phá external avatar và flow cũ
- `[x]` Chỉ đụng `avatar`, không kéo thêm low-risk upload khác khi chưa cần

### Đã làm

- `[x]` Thêm `User.AvatarMediaAssetId`
- `[x]` Thêm navigation `User -> AvatarMediaAsset`
- `[x]` Thêm EF config/index/FK cho `users.avatar_media_asset_id`
- `[x]` Tạo migration `AddUserAvatarMediaAssetLink`
- `[x]` `UserService.UpdateUserProfileAsync` nhận và link `AvatarMediaAssetId`
- `[x]` `UserService.UpdateUserProfileAsync` validate media asset phải thuộc scope `Avatar`
- `[x]` `UserService.UpdateUserProfileAsync` vẫn giữ `AvatarUrl` song song
- `[x]` `CurrentUserResponse` expose `AvatarMediaAssetId`
- `[x]` `UserProfileResponse` expose `AvatarMediaAssetId`
- `[x]` `LoginResponse` expose `AvatarMediaAssetId`
- `[x]` `GoogleLoginResponse` expose `AvatarMediaAssetId`
- `[x]` Frontend profile gửi cả `avatarUrl` và `avatarMediaAssetId`
- `[x]` Thêm helper `toAvatarImageUrl`
- `[x]` Chuyển các avatar call site chính sang helper riêng

### Chưa hoàn chỉnh

- `[~]` `AvatarUrl` vẫn còn là field compatibility chính cho external avatar và render path hiện tại
- `[~]` Chưa có backfill từ avatar legacy cũ sang `AvatarMediaAssetId`
- `[~]` Chưa import Google avatar external vào media core

### Không làm trong phase này

- `[x]` Không xóa `AvatarUrl`
- `[x]` Không đổi Google avatar sync semantics
- `[x]` Không gộp `house rule PDF` vào cùng phase
- `[x]` Không refactor signed/private helper tổng quát

### Tests / Verify

- `[x]` `dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj --no-restore` pass
- `[x]` `dotnet test server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj --no-restore --filter "FullyQualifiedName~UserServiceTests|FullyQualifiedName~BillingServiceTests|FullyQualifiedName~DefaultMediaPermissionServiceTests"` pass
- `[x]` Có test link avatar media asset thành công
- `[x]` Có test reject media asset sai scope
- `[~]` Frontend build tổng chưa dùng làm gate mạnh vì vẫn bị chặn bởi lỗi unrelated ở `react-pdf` preview modules

### Findings / risk mở

- `[!]` Avatar hiện đang ở trạng thái hybrid: vừa có `AvatarMediaAssetId`, vừa giữ `AvatarUrl`
- `[!]` Nếu phase sau muốn chuẩn hóa avatar hoàn toàn vào media core, phải quyết định rõ chiến lược cho Google/external avatar
- `[!]` Snapshot Phase 11 từng có mismatch navigation ở `MeterReading.ProofMediaAsset`; đã sửa đồng bộ trong snapshot/designer để EF migration phase sau chạy ổn

---

## Context Continuity Notes

Khi mở phase mới, luôn copy ít nhất các phần sau từ file này:

- `Global Invariants`
- `Decision Log`
- `Findings / risk mở`
- section phase gần nhất vừa hoàn thành
- `Deferred intentionally`

Nếu AI không biết rõ 4 phần trên, không nên tiếp tục implement phase mới ngay.

---

## Template cập nhật cho phase sau

```md
## Phase X Audit

### Mục tiêu phase
- [x]

### Đã làm
- [x]

### Chưa hoàn chỉnh
- [~]

### Không làm trong phase này
- [x]

### Tests / Verify
- [x]

### Findings / risk mở
- [!]
```

### Checklist cập nhật tối thiểu cho mỗi phase mới

- cập nhật `Current Status`
- thêm section `Phase X Audit`
- cập nhật `Decision Log` nếu có quyết định mới bị khóa
- cập nhật `Findings / risk mở`
- cập nhật `Chưa làm sau Phase 1-2` thành backlog mới nhất nếu phase đó vừa xử lý một phần
