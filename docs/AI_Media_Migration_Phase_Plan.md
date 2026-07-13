# AI Media Migration Phase Plan

## Mục tiêu

Tài liệu này chia kế hoạch chuyển đổi `upload/files/storage` thành nhiều phase nhỏ để:

- không làm tràn context window khi dùng AI
- mỗi phase có phạm vi rõ ràng, dễ review
- mỗi phase có thể bắt đầu bằng một chat mới mà vẫn hiểu đủ ngữ cảnh
- giảm rủi ro AI sửa lan sang module khác
- giữ được chất lượng thông qua checklist, test và review gate

Tài liệu này ưu tiên cách làm `foundation-first`, sau đó migrate từng module một.

## Trạng thái implementation hiện tại

### Mapping to new simplified plan

- `Phase 1 - Thiết kế nền`: đã đạt
- `Phase 2 - Upload/download core`: đã đạt ở media core API/service layer
- `Phase 3 - Tích hợp module public/private`: đã đạt cho các module đang trong scope migration hiện tại ở mức đọc ưu tiên `MediaAssetId` và upload mới đi qua media/cloud path
- `Phase 4 - Bảo mật và audit`: đã đạt ở media core enforcement layer cho validate file, limit dung lượng, kiểm quyền private, audit allow/deny access
- `Phase 5A - Inventory và readiness`: đã materialize inventory tại `docs/AI_Media_Migration_Phase5A_Inventory.md`
- `Phase 5B - Legacy Object Migration/Backfill`: đã có dry-run/report tool và guarded backfill executor để kiểm/link legacy object trước khi cutover
- `Phase 5C - Read Path Cutover`: đã chuyển các read path còn nhạy trong scope 5B sang ưu tiên `MediaAssetId`/media URL và scrub private object key khỏi response khi đã có media id
- `Phase 5D - Legacy Compatibility Guard/Cleanup Prep`: đã đánh dấu deprecation/compatibility cho upload/route object-key cũ và thêm frontend media-id helper để caller mới không mở rộng object-key usage
- `Phase 5E - Local Legacy Data Cleanup`: đã cleanup DB local sau khi user xác nhận dữ liệu demo/local không cần giữ; post-check còn 0 legacy references và 0 storage missing
- `Phase 5F - Legacy API/Frontend Lockdown`: đã chặn legacy upload/admin private object-key route bằng `410 Gone` và bỏ frontend caller/endpoint registry trực tiếp `/api/files`
- `Phase 5G - Schema & Seed Hygiene`: đã dọn runtime seed khỏi fake legacy object keys, thêm/apply migration cleanup sample refs lịch sử, và xác nhận post-check còn 0 legacy references/storage missing
- `Phase 5H - Reset/Re-seed & End-to-End Verification`: đã reset DB local, apply full migration chain, chạy API seed thật, post-reseed storage/readiness report sạch, backend/frontend build pass

Trong file này vẫn giữ phase map chi tiết cũ để tra lịch sử migration, nhưng khi team nói tới
`Phase 1/Phase 2/Phase 3/Phase 4` theo plan mới rút gọn thì phải hiểu như sau:

- `Phase 1` chỉ bao gồm:
  - storage direction
  - `MediaAsset`, `MediaAuditLog`
  - enum media
  - permission matrix nền
  - schema/migration foundation
- `Phase 2` mới bao gồm:
  - upload session
  - finalize upload
  - signed/private download core
  - soft delete flow
  - chuẩn hóa private file contract
- `Phase 3` mới bao gồm:
  - module trong scope đọc ưu tiên `MediaAssetId`
  - upload mới đi qua media workflow/cloud path
  - private/public contract mới được dùng làm đường chính cho module đã migrate
- `Phase 4` mới bao gồm:
  - validate content type + extension theo `MediaScope`
  - giới hạn dung lượng theo `MediaScope`
  - finalize kiểm metadata object thật trong storage
  - private access chặn `PendingUpload`, `Deleted`, và unauthorized user
  - audit thành công và bị từ chối cho view/download-url

Trạng thái code sau khi hoàn thành `Phase 4` mới:

- đã có media workflow API chuẩn:
  - `POST /api/media/upload-url`
  - `PUT /api/media/upload/{mediaAssetId}` fallback backend-proxy
  - `POST /api/media/finalize`
  - `GET /api/media/{mediaAssetId}/download-url`
  - `DELETE /api/media/{mediaAssetId}`
- `S3StorageService` hỗ trợ signed upload URL và signed download URL
- `MediaWorkflowService` chuẩn hóa metadata pending-upload -> uploaded/deleted + audit hook
- `MediaFileValidationPolicy` enforce allowlist type/extension và size limit theo `MediaScope`
- `FinalizeUploadAsync` kiểm object metadata thật từ storage trước khi chuyển sang `Uploaded`
- `MediaController` validate backend-proxy upload theo session metadata trước khi ghi storage
- `MediaAccessService` chặn private asset chưa finalize, đã soft-delete, hoặc user không đủ quyền; deny case có audit log riêng
- compatibility upload cũ vẫn còn giữ song song ở `FilesController`
- một số business flow vẫn còn compatibility/fallback field cũ nên cleanup/rollout vẫn là backlog phase sau
- Phase 5A đã xác nhận chưa sẵn sàng xóa storage/field cũ; cần Phase 5B migration/backfill dry-run trước
- Phase 5B hiện đã có:
  - `LegacyMediaMigrationReadinessService` để quét legacy references theo module
  - console tool `SmartRentalPlatform.MediaMigrationTool`
  - JSON report gồm missing media link, match theo object key, sample lỗi, và optional storage existence check
  - `--mode backfill` mặc định dry-run để lập kế hoạch tạo/link `MediaAsset`
- Phase 5B hiện chưa copy/move object thật
- Phase 5B chỉ update database khi chạy rõ `--mode backfill --dry-run false`; không tự apply trong report/dry-run
- DB local `localhost:5444` ngày 2026-07-13 đã reset sạch, apply migration mới nhất đến `20260713064804_InspectRemainingModelChanges`, chạy Phase 5B backfill apply để tạo/link 3 media asset còn thiếu, rồi dry-run lại sạch với 0 planned creates/links
- Phase 5C đã cutover read path an toàn cho:
  - `RoomingHouseRule` PDF: response chỉ trả `pdfUrl` theo media route khi có `MediaAssetId`; frontend không còn coi `pdfObjectKey` là điều kiện bắt buộc sau upload mới
  - `ContractOccupantDocument`: response dùng private media URL và scrub `Front/Back/ExtraImageObjectKey` khi đã có media id
  - appendix occupant document editor: khi load change JSON có media id, frontend tự dựng private media view URL để preview thay vì cần object key
- Phase 5C vẫn giữ request field/fallback object key để không phá compatibility và chưa xóa storage/endpoint/cột legacy
- Phase 5D đã thêm guard không-breaking:
  - `FilesController` legacy upload trả header `Deprecation`, `X-SRP-Media-Compatibility`, `X-SRP-Media-Replacement`
  - `FileUploadResponse` có `IsCompatibilityResponse` và `CompatibilityWarning`
  - public object-key route `GET /api/media/public/{**objectKey}` có deprecation/replacement headers
  - admin legacy private route `GET /api/admin/media/private?objectKey=...` có deprecation/replacement headers
  - frontend đánh dấu `objectKey` là deprecated trong upload result/type và thêm `toPrivateMediaAssetUrl(mediaAssetId)`
- Phase 5D chưa remove route/field; đây là cleanup prep để các phase sau có tín hiệu runtime và compile-time rõ hơn
- Storage/bucket verification thật ngày 2026-07-13 đã chạy với `--check-storage true`:
  - S3 config local đủ và bucket endpoint reachable
  - DB metadata local sạch: 866/866 legacy references có `MediaAsset` link và match by object key
  - object vật lý missing toàn bộ: 0 present, 866 missing, 0 errors
  - nếu cần giữ dữ liệu local hiện tại thì phải copy/re-upload object vào bucket trước khi xóa legacy route/field
  - nếu dữ liệu local/demo không quan trọng thì đường nhanh hơn là reset/reseed bằng upload mới qua media workflow
- User đã xác nhận dữ liệu local/demo không cần giữ, nên Phase 5E đã thêm và chạy cleanup mode:
  - `phase5e --mode cleanup --dry-run true`: 866 candidates, 855 planned deletes, 1 planned clear
  - `phase5e --mode cleanup --dry-run false`: 855 applied deletes, 1 applied clear
  - post-check bằng `phase5b --check-storage true`: 0 legacy references, 0 storage missing, 0 storage errors
- Phase 5F đã lockdown legacy API/frontend:
  - `POST /api/files/images` và `POST /api/files/pdfs` trả `410 Gone`
  - `GET /api/admin/media/private?objectKey=...` trả `410 Gone`
  - frontend không còn expose/use `ENDPOINTS.FILES`
  - public object-key route vẫn giữ tạm vì public image URL/listing còn phụ thuộc
- Phase 5G đã cleanup seed/schema hygiene:
  - runtime seed không còn tạo fake object keys kiểu `demo/...`, `seed/...`, `/uploads/...`
  - migration data-only `20260713153000_CleanupLegacySampleMediaReferences` cleanup các sample refs lịch sử `demo/%`, `kfc-scenario/%`, `seed/%`, `/uploads/%`
  - migration đã apply trên DB local `localhost:5444`
  - post-check với `phase5b --check-storage true` còn 0 legacy references, 0 storage missing, 0 storage errors
  - legacy columns chưa drop vì DTO/business compatibility/fallback vẫn còn tồn tại
- Phase 5H đã verify reset/reseed:
  - DB local `localhost:5444` đã được drop/reset vì dữ liệu local không cần giữ
  - full migration chain apply từ đầu tới `20260713153000_CleanupLegacySampleMediaReferences`
  - API startup seed đã chạy với development + wallet QA seed enabled
  - post-reseed report `phase5b --check-storage true` còn 0 legacy references, 0 missing media links, 0 storage missing/errors
  - `dotnet build`, media/migration unit tests, và `npm.cmd run build` đều pass
  - chưa chạy manual/browser E2E upload qua UI trong phase này

### Historical detailed plan status

Theo code hiện có trong repo:

- `Phase 1 - Media schema foundation` đã được implement
- `Phase 2 - Media service foundation` đã được implement ở mức foundation/skeleton
- `Phase 3 - Compatibility upload adapter` đã được implement ở mức public upload compatibility
- `Phase 4 - Contract file migration` đã được implement cho contract main file
- `Phase 5 - Contract appendix file migration` đã được implement cho appendix raw/masked file generation và access permission
- `Phase 6 - KYC media migration` đã được implement cho upload + schema + admin read-model preparation
- `Phase 7 - Admin private media access + audit` đã được implement
- `Phase 8 - Legal document migration` đã được implement
- `Phase 9 - Public property image migration backend` đã được implement
- `Phase 10 - Public property image migration frontend` đã được implement ở mức public image consumption
- `Phase 11 - Billing proof image migration` đã được implement ở mức backend/schema/permission foundation
- `Phase 12 - Shared frontend asset API cleanup` đã được implement một phần theo hướng safe cleanup cho public/shared image helpers
- `Phase 13 - Avatar and low-risk uploads` đã được implement cho avatar media linkage theo hướng compatibility

Các điểm chưa hoàn chỉnh nhưng đã biết từ code hiện tại:

- runtime storage hiện đã đi qua `S3StorageService` cho cả `IMediaStorageService` và `IPrivateStorageService`
- signed download URL đã có ở storage layer và media API core, nhưng nhiều business flow private hiện vẫn đang chủ yếu dùng `OpenReadAsync`
- permission matrix mới chưa phủ hết mọi module private còn lại
- audit semantics cho read/download chưa được chốt hoàn toàn
- business module còn lại phần lớn vẫn chưa lưu `MediaAssetId`
- upload compatibility vẫn đang trả contract cũ `ObjectKey/Url`
- contract appendix chưa có migration/backfill cho dữ liệu appendix legacy cũ
- legal document object cũ có thể vẫn đang nằm ở public object key cũ ở tầng storage thật dù metadata đã được migrate sang private semantics
- public property image route vẫn đang mở theo `objectKey`; frontend hiện đã ưu tiên `imageUrl` backend trả về nhưng fetch layer public chưa chuyển hẳn sang `MediaAssetId`
- billing proof image hiện đã có `ProofMediaAssetId`, private access rule và backfill metadata; frontend upload/viewer chuyên biệt vẫn chưa được materialize
- shared helper frontend hiện đã được tách rõ hơn cho public listing/property image, nhưng helper private/transitional vẫn còn giữ compatibility cũ để tránh lan sửa
- avatar user-uploaded hiện đã có `AvatarMediaAssetId`, nhưng `AvatarUrl` vẫn được giữ song song để tương thích với external avatar và call site cũ
- các mục trên không chặn `Phase 1` theo plan mới rút gọn; sau khi `Phase 2` core đã xong, chúng trở thành backlog rollout/cleanup phase sau

Nếu tiếp tục bám đúng code hiện tại, phase kế tiếp mặc định là:

- theo plan cũ chi tiết: `Phase 14 - Chat attachment design stub hoặc implementation`
- theo plan mới rút gọn: sau Phase 5H, bước tiếp theo là `Phase 5I - Legacy Schema/API Removal Plan`, tức đối chiếu caller theo từng module trước khi drop legacy columns/routes/helpers

---

## Nguyên tắc vận hành với AI

### 1. Scope rất hẹp

Mỗi chat/phase chỉ nên giải quyết một trong các loại việc sau:

- thêm schema + entity + configuration
- thêm service/interface
- migrate 1 module backend
- migrate 1 cụm frontend
- viết tests cho 1 phần
- cleanup field cũ sau khi đã migrate xong

Không gộp nhiều loại việc trong cùng một chat.

### 2. Luôn có bất biến nghiệp vụ

Trong mỗi chat mới, phải nhắc lại:

- business rule nào không được đổi
- endpoint nào không được phá vỡ compatibility
- file nào được sửa
- file nào không được đụng

### 3. Output nhỏ, review dễ

Mỗi phase chỉ nên tạo PR/diff nhỏ đến vừa.

Mục tiêu:

- dễ đọc diff
- dễ build
- dễ test
- dễ rollback nếu sai

### 4. Không xóa code cũ quá sớm

Trong giai đoạn migration:

- thêm field mới trước
- backfill dữ liệu
- chuyển read path sang field mới
- giữ fallback field cũ
- chỉ xóa field cũ sau cùng

### 5. Mỗi phase phải có chất lượng gate

Mỗi phase xong phải có:

- build pass
- test liên quan pass
- review diff
- cập nhật tài liệu phase status

---

## Kiến trúc đích đến

Cần chốt rõ trước khi bắt đầu:

- `MediaAsset`
- `MediaAuditLog`
- `MediaScope`
- `MediaVisibility`
- `MediaStatus`
- `IMediaStorageService`
- `IMediaAccessService`
- `IMediaPermissionService`
- `MediaController` hoặc nhóm endpoint media chung

### Bất biến cần giữ

- file private không có public URL cố định
- database chỉ lưu metadata, không lưu binary
- object key không dùng tên file gốc
- truy cập private phải qua backend hoặc signed access ngắn hạn
- file contract đã ký không bị ghi đè
- logic public/private phụ thuộc vào metadata và trạng thái nghiệp vụ

---

## Quy ước mở chat mới

Mỗi phase bắt đầu chat mới, paste template sau:

```text
Phase: <tên phase>
Mục tiêu: <1 mục tiêu duy nhất>
Phạm vi được sửa: <danh sách file/folder/module>
Không được sửa: <danh sách file/folder/module>
Bất biến nghiệp vụ:
- ...
- ...
Kết quả mong muốn:
- ...
Yêu cầu chất lượng:
- diff nhỏ
- build pass
- test liên quan pass
- không xóa fallback cũ nếu chưa được yêu cầu
```

### Quy tắc context cho mỗi chat

Chỉ đưa vào chat mới:

- file này
- phase hiện tại
- 3-7 file liên quan trực tiếp
- không nạp toàn bộ codebase vào context

---

## Phase map tổng thể

1. Phase 0 - Baseline và inventory
2. Phase 1 - Media schema foundation
3. Phase 2 - Media service foundation
4. Phase 3 - Compatibility upload adapter
5. Phase 4 - Contract file migration
6. Phase 5 - Contract appendix file migration
7. Phase 6 - KYC media migration
8. Phase 7 - Admin private media access + audit
9. Phase 8 - Legal document migration
10. Phase 9 - Public property image migration backend
11. Phase 10 - Public property image migration frontend
12. Phase 11 - Billing proof image migration
13. Phase 12 - Shared frontend asset API cleanup
14. Phase 13 - Avatar and low-risk uploads
15. Phase 14 - Chat attachment design stub hoặc implementation
16. Phase 15 - Backfill cleanup + remove legacy fields
17. Phase 16 - Regression test + hardening + docs handoff

---

## Phase 0 - Baseline và inventory

### Mục tiêu

Chốt mặt bằng hiện tại để các phase sau không phải lòng vòng tìm lại context.

### Đầu vào cần đọc

- entity đang giữ `ObjectKey`, `ImageUrl`, `FileUrl`
- endpoint upload/download hiện tại
- helper frontend `toAssetUrl`
- flow contract, KYC, legal document, billing proof image

### Việc cần làm

- tạo bảng inventory:
  - module
  - current fields
  - current endpoint
  - public/private
  - business rule nhạy cảm
- tạo danh sách migration targets
- tạo danh sách field cũ cần backfill sau này

### Không làm trong phase này

- không thêm schema mới
- không refactor code

### Deliverable

- 1 file markdown inventory
- 1 checklist migration target

### Trạng thái thực tế hiện tại

- plan này đã tồn tại
- audit checklist đã tồn tại
- context guardrail đã tồn tại
- nhưng inventory doc riêng và migration target checklist riêng chưa được materialize thành artifact độc lập

### Gate

- inventory đủ để mỗi phase sau chỉ cần đọc lại phase này và phase của nó

---

## Phase 1 - Media schema foundation

### Mục tiêu

Thêm các entity và enum nền tảng, chưa đụng vào module cũ.

### Scope

- `Domain`
- `Infrastructure/Persistence/Configurations`
- migration
- contracts nội bộ nếu cần

### Việc cần làm

- thêm `MediaAsset`
- thêm `MediaAuditLog`
- thêm enum:
  - `MediaScope`
  - `MediaVisibility`
  - `MediaStatus`
- thêm DB configuration
- thêm migration

### Không được làm

- không sửa contract module nghiệp vụ
- không sửa frontend
- không chuyển module cũ sang dùng field mới

### AI prompt focus

```text
Chỉ tạo entity, enum, EF configuration và migration cho media core.
Không sửa business service hiện có.
Không sửa frontend.
```

### Gate

- migration tạo bảng thành công
- build pass
- naming thống nhất

---

## Phase 2 - Media service foundation

### Mục tiêu

Tạo service layer dùng để lưu trữ, metadata, access, audit.

### Scope

- interfaces application common/media
- infrastructure storage implementation mới
- service permission/access skeleton

### Việc cần làm

- `IMediaStorageService`
- `IMediaAssetService`
- `IMediaAccessService`
- `IMediaPermissionService`
- object key generation strategy
- metadata creation flow
- audit log hook skeleton

### Không được làm

- không refactor module business
- không đổi endpoint cũ

### Gate

- unit tests cho object key generation
- unit tests cho metadata create
- build pass

---

## Phase 3 - Compatibility upload adapter

### Mục tiêu

Giữ endpoint upload cũ hoạt động nhưng bên dưới đã dùng media core.

### Scope

- `FilesController`
- upload response mapper
- adapter service

### Việc cần làm

- map upload cũ -> tạo `MediaAsset`
- tạm thời vẫn trả response gần giống cũ nếu cần
- lưu `objectKey/url` compatibility output nếu cần

### Không được làm

- không đổi frontend hàng loạt
- không xóa endpoint cũ

### Gate

- upload cũ vẫn chạy
- DB mới có `MediaAsset`
- không phá module hiện tại

### Trạng thái thực tế hiện tại

- `IFileStorageService` đã được chuyển sang adapter media-backed
- upload cũ vẫn trả `ObjectKey` và `Url`
- `FilesController` không cần đổi contract
- mới chỉ cover compatibility flow cho public upload hiện tại, chưa đổi business module sang `MediaAssetId`
- lưu ý: phase này là `Phase 2` của plan chi tiết cũ, không tương đương `Phase 2` trong plan mới rút gọn

---

## Phase 4 - Contract file migration

### Mục tiêu

Chuyển contract file sang `MediaAssetId` với thay đổi nghiệp vụ tối thiểu.

### Scope

- `ContractFile`
- `ContractFileService`
- `RentalContractsController`
- response file contract

### Việc cần làm

- thêm `MediaAssetId` vào `ContractFile`
- backfill dữ liệu cho file cũ
- upload/generate contract file tạo `MediaAsset`
- `OpenFileAsync` đọc qua media access service
- thêm `FileHash` nếu chốt làm ngay phase này

### Business rule phải giữ

- chỉ landlord hoặc main tenant hợp lệ mới generate raw contract
- contract phải active và đủ chữ ký
- occupant chỉ xem masked nếu được phép

### Không được làm

- không đổi workflow ký contract
- không đổi logic permission contract ngoài phạm vi file access

### Gate

- service tests cho permission file
- integration test download file
- build pass

### Kế hoạch chi tiết

- xem thêm `docs/AI_Media_Migration_Phase4_ContractFile_Plan.md`

### Trạng thái thực tế hiện tại

- `ContractFile` main file đã có `MediaAssetId`
- generate contract PDF mới đã tạo `MediaAsset` private
- `OpenFileAsync` đã ưu tiên media layer và giữ fallback legacy
- `ContractFileResponse` đã có `MediaAssetId`
- appendix file vẫn chưa migrate trong phase này

---

## Phase 5 - Contract appendix file migration

### Mục tiêu

Tách riêng appendix vì logic của nó lớn, không gộp chung với contract file.

### Scope

- `ContractAppendixService`
- `ContractFile` cho file appendix
- appendix response mapping

### Việc cần làm

- appendix files dùng `MediaAssetId`
- generate appendix file qua media core
- giữ logic raw/masked như hiện tại
- thêm audit cho private file access nếu đã có access service hoàn chỉnh

### Business rule phải giữ

- pending signature / active / rejected logic không đổi
- permission appendix không bị nới lỏng

### Gate

- appendix preview không đổi
- appendix download không đổi nghiệp vụ
- tests liên quan pass

### Trạng thái thực tế hiện tại

- `ContractAppendixService` đã generate raw/masked appendix PDF qua media core
- `ContractFile` appendix row mới đã lưu `MediaAssetId` và vẫn giữ `StorageObjectKey`
- `DefaultMediaPermissionService` đã hiểu private access cho appendix raw/masked file
- chưa có migration/backfill cho appendix file cũ đang thiếu `MediaAssetId`
- đường private access thực tế vẫn đi qua `OpenReadAsync`, chưa chuyển sang signed download URL

---

## Phase 6 - KYC media migration

### Mục tiêu

Chuyển 3 file KYC sang `MediaAssetId`, giữ nguyên logic eKYC.

### Scope

- `KycVerification`
- `KycService`
- admin KYC read model nếu cần

### Việc cần làm

- thêm `FrontMediaAssetId`, `BackMediaAssetId`, `SelfieMediaAssetId`
- backfill từ object key cũ
- upload KYC tạo media asset private
- provider integration vẫn có được object stream/object key cần thiết thông qua media service

### Business rule phải giữ

- chặn duplicate approved KYC
- duplicate citizen ID vẫn hoạt động
- onboarding status logic giữ nguyên

### Không được làm

- không đổi risk calculation
- không đổi approval workflow

### Gate

- submit KYC vẫn chạy
- admin vẫn xem được file
- test KYC service pass

### Trạng thái thực tế hiện tại

- `KycVerification` đã có `FrontMediaAssetId`, `BackMediaAssetId`, `SelfieMediaAssetId`
- `KycService` đã upload file KYC qua media core và vẫn truyền `objectKey` cho VNPT eKYC client
- migration đã backfill KYC legacy từ `FrontImageObjectKey`, `BackImageObjectKey`, `SelfieImageObjectKey` sang `media_assets`
- `AdminKycDetailResponse` và `AdminKycInfo` đã expose thêm `MediaAssetId` cho 3 file KYC
- admin KYC/private viewer vẫn đang dùng URL `objectKey` cũ ở phase này để giữ compatibility

---

## Phase 7 - Admin private media access + audit

### Mục tiêu

Chuẩn hóa việc admin view/download private file qua media layer chung.

### Scope

- `AdminMediaController`
- admin KYC/legal document viewers
- audit logging

### Việc cần làm

- không dùng query `objectKey` nữa
- dùng `mediaAssetId` hoặc access token ngắn hạn
- ghi audit `View` và `Download`
- tách inline open và forced download nếu cần

### Business rule phải giữ

- chỉ admin được xem file theo role/rule
- tenant/guest không được truy cập chéo

### Gate

- audit records được tạo
- admin pages vẫn load được

### Trạng thái hiện tại

- `AdminMediaController` đã có route media-asset based:
  - `GET /api/admin/media/private/{mediaAssetId}`
  - `GET /api/admin/media/private/{mediaAssetId}/download`
- admin KYC responses đã build URL theo `mediaAssetId` thay vì `objectKey`
- `MediaAccessService` đã ghi audit `View`/`Download` với `IpAddress`, `UserAgent`, `MetadataJson`
- `DefaultMediaPermissionService` đã allow admin truy cập private media qua media core
- legacy endpoint `GET /api/admin/media/private?objectKey=...` vẫn còn giữ tạm cho module legal document chưa migrate xong, sẽ dọn ở Phase 8+

---

## Phase 8 - Legal document migration

### Mục tiêu

Chuyển giấy tờ pháp lý khu trọ sang media core.

### Scope

- `RoomingHouseLegalDocument`
- `RoomingHouseMediaService`
- admin rooming house approval response
- landlord legal document UI contracts

### Việc cần làm

- thêm `FrontMediaAssetId`, `BackMediaAssetId`, `ExtraMediaAssetId`
- backfill field cũ
- cập nhật service save/legal read
- admin xem qua media access chung

### Business rule phải giữ

- chỉ sửa khi `Draft` hoặc `Rejected`
- tenant/guest không được xem

### Gate

- legal doc upload/save vẫn hoạt động
- admin duyệt vẫn mở được file

### Trạng thái hiện tại

- `RoomingHouseLegalDocument` đã có:
  - `FrontMediaAssetId`
  - `BackMediaAssetId`
  - `ExtraMediaAssetId`
- `RoomingHouseMediaService.UpdateLegalDocumentAsync` đã link legal doc sang `media_assets`
- `RoomingHouseDetailResponse.LegalDocument` đã có thêm:
  - `Front/Back/ExtraMediaAssetId`
  - `Front/Back/ExtraImageUrl`
- `AdminRoomingHouseApprovalService` đã map legal doc sang private media URL chung `/api/media/private/{mediaAssetId}`
- `MediaController` đã có authenticated route `GET /api/media/private/{mediaAssetId}`
- `DefaultMediaPermissionService` đã cho phép:
  - landlord sở hữu khu trọ xem legal-document media
  - admin xem legal-document media
- upload `FileUploadScope.LegalDocument` mới đã tạo private media object cho các upload phát sinh sau Phase 8
- migration `AddRoomingHouseLegalDocumentMediaAssets` đã backfill legal doc cũ sang `media_assets`
- legacy field `FrontImageObjectKey`, `BackImageObjectKey`, `ExtraImageObjectKey` vẫn được giữ để compatibility

---

## Phase 9 - Public property image migration backend

### Mục tiêu

Backend chuyển sang `MediaAssetId` nhưng vẫn có response an toàn cho frontend.

### Scope

- `PropertyImage`
- `RoomingHouseMediaService`
- `RoomReadModelMapper`
- responses cho room/house images

### Việc cần làm

- thêm `MediaAssetId`
- backfill từ image cũ
- mapper vẫn có thể trả `imageUrl`
- backend không tự tin rằng `/uploads/{objectKey}` là source of truth nữa

### Business rule phải giữ

- cover image rule
- min 3 images rule
- public chỉ sau approved/published

### Gate

- frontend cũ chưa cần đổi nhiều vẫn chạy

### Trạng thái hiện tại

- `PropertyImage` đã có `MediaAssetId`
- `RoomingHouseMediaService.UpdateImagesAsync` đã link ảnh khu trọ sang `media_assets`
- `RoomMediaService.UpdateImagesAsync` đã link ảnh phòng sang `media_assets`
- `PropertyImageResponse` đã expose `MediaAssetId`
- `RoomingHouseDetailResponse`, `RoomResponse`, `AdminRoomingHouseDetailResponse` vẫn trả `ImageUrl` compatibility cho frontend cũ
- migration `AddPropertyImageMediaAssets` đã backfill property image legacy sang `media_assets`
- `ObjectKey` và `ImageUrl` legacy vẫn được giữ để compatibility

---

## Phase 10 - Public property image migration frontend

### Mục tiêu

Dọn frontend public image theo API mới, giảm phụ thuộc `toAssetUrl(objectKey)`.

### Scope

- rooming house editor/gallery
- public detail pages
- listing cards
- helper asset API

### Việc cần làm

- tách public asset helper và private asset helper
- ưu tiên dùng `imageUrl` backend trả về
- giảm các nơi tự lắp `/uploads/...`

### Không được làm

- không đổi layout/UI không cần thiết
- không refactor CSS không liên quan

### Gate

- gallery/load image pass
- không còn hardcoded path private/public bị trộn lẫn

### Trạng thái hiện tại

- frontend đã có helper `toPublicAssetUrl(imageUrl, objectKey)` cho public property image
- `HouseImageGallery` đã ưu tiên `imageUrl` backend trả về thay vì tự ghép URL từ `objectKey`
- `PropertyImageEditor` đã giữ `mediaAssetId` trong state upload/result
- `PublicRoomingHouseDetailPage` đã dùng helper public riêng cho room image preview
- `PropertyImageRequest` và `PropertyImage` client types đã hiểu `mediaAssetId`
- helper generic `toAssetUrl` vẫn còn để giữ compatibility cho avatar/legal/pdf/private-path cũ

---

## Phase 11 - Billing proof image migration

### Mục tiêu

Đây là phase có đổi logic nghiệp vụ rõ ràng nhất trong file-related modules.

### Scope

- `MeterReading`
- `BillingService`
- landlord billing UI
- tenant invoice UI

### Việc cần làm

- thêm `ProofMediaAssetId`
- backfill field cũ
- tạo media scope riêng cho meter proof
- thêm rule theo đích cuối:
  - tenant/occupant chỉ xem khi invoice đã publish ra khỏi trạng thái `Draft`
  - rule chặn thay ảnh sau `Paid` chỉ áp dụng khi phase sau thật sự thêm flow update proof riêng
  - nếu cần sửa sau khi invoice đã finalized thì ưu tiên evidence/adjustment mới thay vì mutate trực tiếp proof cũ

### Business rule phải giữ

- invoice generation logic hiện tại không bị phá
- landlord ownership checks giữ nguyên

### Gate

- tests permission tối thiểu theo `Draft` và một trạng thái published như `Issued`
- test tenant access
- nếu có flow update proof riêng thì mới thêm test landlord update block sau `Paid`

### Trạng thái hiện tại

- `MeterReading` đã có `ProofMediaAssetId`
- migration `AddMeterReadingProofMediaAssets` đã thêm cột mới và backfill từ `ProofImageObjectKey` sang `media_assets`
- `BillingService` đã link proof image vào `MediaAsset` khi tạo meter reading mới
- `LatestMeterReadingResponse` đã expose:
  - `ProofMediaAssetId`
  - `ProofImageUrl`
- `InvoiceItemResponse` đã expose:
  - `MeterReadingProofMediaAssetId`
  - `MeterReadingProofImageUrl`
- `DefaultMediaPermissionService` đã cover private access cho meter reading proof:
  - landlord được xem
  - tenant/occupant chỉ xem khi invoice không còn `Draft`
- upload scope `MeterReading` đã được thêm vào compatibility upload layer và map sang private media
- frontend hiện mới được cập nhật typing cho phase này; chưa có meter-proof uploader/viewer hoàn chỉnh
- chưa có flow update proof image riêng sau khi invoice đã tạo, nên rule “không cho thay ảnh sau `Paid`” mới dừng ở mức design note, chưa phải enforcement hiện hành

---

## Phase 12 - Shared frontend asset API cleanup

### Mục tiêu

Dọn helper chung sau khi đã migrate phần lớn module.

### Scope

- `toAssetUrl`
- các shared file upload/download helper

### Việc cần làm

- tách helper:
  - public display
  - private fetch/open
- loại bỏ assumptions cũ
- cập nhật typing frontend

### Gate

- không còn helper chung gây nhầm private/public

### Trạng thái hiện tại

- `client/src/shared/api/assets.ts` đã có helper tách ngữ nghĩa rõ hơn:
  - `toPublicListingImageUrl`
  - `toPublicPropertyImageUrl`
- một số màn public/shared đã chuyển sang helper mới:
  - `SearchRoomingHousesPage`
  - `PublicRoomingHouseDetailPage`
  - `HouseImageGallery`
  - `MePage` phần public listing cards
  - `LandlordDashboardPage` phần cover image
  - `RentalAiChatbot` mini house cards
- `toAssetUrl` vẫn được giữ cho avatar và các flow transitional/private để tránh đụng vào module chưa chốt
- phase này chưa dọn:
  - `houseRule` PDF
  - legal/KYC/admin private image rendering
  - avatar/profile image flow
  - private download/open helper semantics

---

## Phase 13 - Avatar and low-risk uploads

### Mục tiêu

Migrate các phần ít rủi ro cuối để tránh làm bẩn context sớm.

### Scope

- avatar
- house rule PDF nếu cần
- low-risk uploads khác

### Gate

- profile page vẫn hoạt động
- upload low-risk vẫn chạy

### Trạng thái code hiện tại

- upload avatar đã đi qua `MediaBackedFileStorageService` với:
  - `FileUploadScope.Avatar`
  - `MediaScope.Avatar`
  - `mediaAssetId` trong upload response
- nhưng business entity `User` hiện vẫn chỉ lưu `AvatarUrl`
- frontend profile hiện upload avatar xong lưu `uploadResult.url` vào `avatarUrl`
- các màn hiển thị avatar hiện vẫn dùng `toAssetUrl`
- Google login vẫn có thể set `AvatarUrl` thành external provider URL

### Kế hoạch an toàn được chốt

- Phase 13 mặc định chỉ làm `avatar`
- thêm `AvatarMediaAssetId` vào `User` theo hướng compatibility:
  - giữ `AvatarUrl`
  - không ép Google avatar phải có `MediaAssetId`
- chưa gộp `house rule PDF` vào phase này trừ khi có nhu cầu rõ ràng sau

### Trạng thái hiện tại

- `User` đã có `AvatarMediaAssetId`
- migration `AddUserAvatarMediaAssetLink` đã thêm:
  - cột `users.avatar_media_asset_id`
  - index
  - FK sang `media_assets`
- `UserService.UpdateUserProfileAsync` đã:
  - nhận `AvatarMediaAssetId`
  - validate scope `Avatar`
  - link metadata về `User`
  - vẫn giữ `AvatarUrl`
- `CurrentUserResponse`, `UserProfileResponse`, `LoginResponse`, `GoogleLoginResponse` đã expose `AvatarMediaAssetId`
- `ProfileInfoPage` đã gửi cả:
  - `avatarUrl`
  - `avatarMediaAssetId`
- shared frontend đã có `toAvatarImageUrl`
- các avatar call site chính đã chuyển sang helper riêng:
  - `HomeHeader`
  - `AccountLayout`
  - `MePage`
  - `TenantInvoicesPage`
  - `ProfileInfoPage`
- Google/external avatar flow vẫn giữ compatibility bằng `AvatarUrl`

---

## Phase 14 - Chat attachment design stub hoặc implementation

### Mục tiêu

Không nên làm sớm nếu module chat chưa chốt schema.

### Lựa chọn A - Nếu chat chưa sẵn sàng

- chỉ viết design doc
- chốt schema attachment
- chốt permission matrix
- chốt API draft

### Lựa chọn B - Nếu chat đã sẵn sàng

- thêm attachment entity
- thêm media scope `ChatAttachment`
- thêm permission theo conversation
- thêm download flow private

### Gate

- không couple vào AI chatbot conversation cache
- rule conversation ownership rõ ràng

---

## Phase 15 - Backfill cleanup + remove legacy fields

### Mục tiêu

Chỉ bắt đầu khi đã migrate xong các module chính.

### Việc cần làm

- tắt read fallback field cũ
- xóa field cũ:
  - `ObjectKey`
  - `ImageUrl`
  - `FileUrl`
  - `ProofImageObjectKey`
  - các `FrontImageObjectKey/...` nếu đã thay hoàn toàn
- cập nhật seed/demo data
- cập nhật tests

### Điều kiện trước khi bắt đầu

- tất cả module đang đọc field mới
- không còn frontend nào cần field cũ
- backfill đã chạy xong

### Gate

- build pass
- regression tests pass

---

## Phase 16 - Regression test + hardening + docs handoff

### Mục tiêu

Đóng phase và chốt handoff cho team.

### Việc cần làm

- test matrix:
  - upload valid
  - upload invalid type
  - upload oversize
  - permission private deny/allow
  - audit log
  - soft delete
  - contract immutability
  - billing proof visibility
- cập nhật docs cho team
- viết hướng dẫn sử dụng `MediaAssetId`

### Deliverable

- test checklist
- migration note
- dev guide ngắn cho các module khác

---

## Mỗi phase nên tạo chat mới như thế nào

### Rule 1

Mỗi chat mới chỉ đọc:

- file này
- phase hiện tại
- 1 phase trước nếu cần
- 3-7 file code liên quan trực tiếp

### Rule 2

Không dump toàn bộ codebase vào chat.

### Rule 3

Nếu phase có backend + frontend, tách thành 2 chat nếu có thể.

### Rule 4

Nếu phase có schema + business service, tách 2 chat:

- chat A: schema/config/migration
- chat B: service/business integration

---

## Prompt template cho mỗi phase

```text
Đang làm Phase <số>: <tên phase>.

Mục tiêu duy nhất:
<viết 1 mục tiêu>

Chỉ được sửa:
- <file 1>
- <file 2>

Không được sửa:
- <file 3>
- <folder 1>

Bất biến nghiệp vụ phải giữ:
- <rule 1>
- <rule 2>

Yêu cầu kỹ thuật:
- giữ compatibility nếu field cũ chưa được bỏ
- không xóa fallback nếu chưa được yêu cầu
- ưu tiên diff nhỏ
- build pass
- thêm test liên quan nếu có logic mới

Kết quả mong muốn:
- <output 1>
- <output 2>
```

---

## Checklist review sau mỗi phase

- diff có nhỏ và đúng phạm vi không
- AI có đổi business rule ngoài yêu cầu không
- có thêm null safety và fallback hợp lý không
- build có pass không
- test liên quan có pass không
- API contract có bị phá không
- có còn hardcode `/uploads/` không đang ở private flow
- có log audit nếu phase yêu cầu không
- có note lại những việc phase sau cần tiếp tục không

---

## Dấu hiệu context window đang bắt đầu quá tải

Nếu trong chat xuất hiện các dấu hiệu sau, phải dừng và mở chat mới:

- AI bắt đầu sửa nhiều module cùng lúc
- AI lặp lại thông tin cũ, mất track invariant
- diff quá lớn, không đọc được
- cần nạp quá nhiều file mới hiểu được
- AI bắt đầu đề xuất refactor ngoài scope

Khi đó:

1. dừng chat hiện tại
2. commit hoặc ghi lại status
3. mở chat mới với phase hoặc sub-phase nhỏ hơn

---

## Chia nhỏ thêm nếu team cần

Nếu phase vẫn còn lớn, có thể tách như sau:

- Phase 4A: schema contract file
- Phase 4B: contract file service
- Phase 4C: contract file tests

- Phase 6A: KYC schema
- Phase 6B: KYC upload integration
- Phase 6C: admin KYC viewer

- Phase 11A: meter proof schema
- Phase 11B: landlord upload/update rule
- Phase 11C: tenant view rule

---

## Kết luận

Kế hoạch này được thiết kế để AI không phải ôm quá nhiều context trong một chat.

Nguyên tắc chính là:

- xây nền trước
- migrate từng module sau
- mỗi phase một mục tiêu duy nhất
- mỗi phase có gate chất lượng rõ ràng
- mỗi phase có thể mở chat mới độc lập mà vẫn đủ hiệu quả
