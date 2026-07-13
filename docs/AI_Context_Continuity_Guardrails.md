# AI Context Continuity Guardrails

## Mục tiêu

File này dùng để giảm 2 rủi ro khi làm nhiều phase với AI:

- `context window bị tràn`
- `sau khi condense, AI mất chi tiết quan trọng và bắt đầu lệch khỏi design ban đầu`

Tài liệu này đi kèm với `docs/AI_Media_Migration_Phase_Plan.md`.

## Current status

- Done:
  - phase cũ theo lộ trình chi tiết đã đi đến avatar/media-link coverage
  - nếu đối chiếu theo plan mới rút gọn, `Phase 1 - Thiết kế nền` đã đạt
  - media core hiện đã có API chuẩn cho `upload-url/finalize/download-url/soft-delete`
  - theo plan mới, `Phase 3 - Tích hợp module public/private` đã đạt cho module trong scope hiện tại
  - theo plan mới, `Phase 4 - Bảo mật và audit` đã enforce validate file, size limit, storage metadata check, private access deny, và audit allow/deny ở media core
  - theo plan mới, `Phase 5A - Inventory và readiness` đã materialize tại `docs/AI_Media_Migration_Phase5A_Inventory.md`
  - theo plan mới, `Phase 5B - Legacy Object Migration/Backfill` đã có report tool và guarded backfill executor
  - theo plan mới, `Phase 5C - Read Path Cutover` đã chuyển scope sau 5B sang media-id-first read path và scrub private object key trong response khi đã có `MediaAssetId`
  - theo plan mới, `Phase 5D - Legacy Compatibility Guard/Cleanup Prep` đã thêm deprecation/compatibility headers cho legacy upload/object-key routes và frontend helper media-id-first
  - storage bucket verification thật đã chạy ngày 2026-07-13 với S3 config local: bucket reachable, DB metadata clean, nhưng 866/866 legacy objects missing trong bucket
  - theo plan mới, `Phase 5E - Local legacy data cleanup` đã thêm `phase5e --mode cleanup` vào migration tool, apply trên DB local `localhost:5444`, và post-check còn 0 legacy references / 0 storage missing
  - theo plan mới, `Phase 5F - Legacy API/Frontend Lockdown` đã chặn legacy upload bằng `410 Gone`, chặn admin private object-key route bằng `410 Gone`, và bỏ frontend endpoint registry/caller trực tiếp `/api/files`
  - theo plan mới, `Phase 5G - Schema & Seed Hygiene` đã dọn runtime seed khỏi fake legacy object keys, thêm/apply data-only cleanup migration `20260713153000_CleanupLegacySampleMediaReferences`, và post-check còn 0 legacy references / 0 storage missing
  - theo plan mới, `Phase 5H - Reset/Re-seed & End-to-End Verification` đã drop/reset DB local `localhost:5444`, apply full migration chain từ đầu, chạy API seed thật, và post-reseed check còn 0 legacy references / 0 storage missing
- Not done:
  - business modules toàn hệ thống vẫn chưa chuyển hết sang media workflow mới
  - replace/approve/reject audit chưa được chuẩn hóa toàn hệ thống nếu không đi qua business service riêng
  - chưa xóa fallback field/storage/endpoint cũ khỏi code/schema toàn hệ thống; schema legacy columns chưa drop vì DTO/business compatibility vẫn còn dùng
- Follow-up:
  - mọi chat tiếp theo phải phân biệt rõ `phase cũ nhiều bước` và `phase mới rút gọn nhiều phase`
  - phase tiếp theo theo plan mới là lập plan drop legacy schema/API theo từng module, chưa xóa hàng loạt nếu chưa đối chiếu caller
- Known risks:
  - không được hiểu nhầm rằng việc có media API mới đồng nghĩa toàn bộ business flow đã bỏ compatibility upload cũ
  - không được hiểu nhầm `Phase 4` cũ trong historical plan là cùng scope với `Phase 4 - Bảo mật và audit` của plan mới

---

## 1. Luôn có một nguồn sự thật ngắn và ổn định

Trước khi mở phase mới, luôn chuẩn bị 3 artifact:

1. `phase plan`
2. `phase status`
3. `phase invariants`

Với repo hiện tại, artifact thực tế đang có là:

- `docs/AI_Media_Migration_Phase_Plan.md`
- `docs/AI_Media_Migration_Audit_Checklist.md`
- `docs/AI_Context_Continuity_Guardrails.md`

Artifact `inventory` của Phase 0 hiện chưa được tách thành file riêng, nên nếu cần inventory chi tiết phải ghi rõ đang lấy từ plan/audit hay từ code.

### 1.1 Phase plan

Mỗi phase chỉ có 1 mục tiêu chính, 3-7 file liên quan trực tiếp và 1 checklist test ngắn.

### 1.2 Phase status

Sau mỗi phase, cập nhật một block ngắn:

```md
## Current status
- Done:
- Not done:
- Follow-up:
- Known risks:
```

### 1.3 Phase invariants

Luôn có một block cố định để AI không drift:

```md
## Invariants
- Không đổi endpoint cũ
- Không xóa field cũ
- Private file không có public URL cố định
- DB chỉ lưu metadata, không lưu binary
```

Nếu phải condense, block này phải được giữ nguyên verbatim.

---

## 2. Mỗi chat mới phải có “handoff capsule”

Khi context dài, không tiếp tục chat cũ vô hạn. Tạo chat mới với capsule ngắn:

```text
Phase:
Mục tiêu duy nhất:
Đã làm xong:
Chưa làm:
Scope được sửa:
Không được sửa:
Invariants:
Test đã pass:
Rủi ro còn lại:
```

Capsule này phải ngắn nhưng quyết định-complete.

---

## 3. Không để AI tự suy luận lại kiến trúc từ đầu

Mỗi khi chuyển phase, luôn nêu lại các quyết định đã chốt:

- `MediaAsset` là metadata source of truth
- `MediaAuditLog` là audit source
- `MediaScope`, `MediaVisibility`, `MediaStatus` đã chốt
- object key format đã chốt
- public/private semantics đã chốt
- fallback cũ chưa được xóa
- trong plan mới rút gọn:
  - `Phase 1` chỉ gồm nền tảng
  - `Phase 2` mới gồm upload session/finalize/download/signed URL/soft delete core
  - `Phase 3` mới gồm module integration cho scope hiện tại
  - `Phase 4` mới gồm security/audit enforcement ở media core
  - `Phase 5A` chỉ gồm inventory/readiness, không migration/cutover
  - `Phase 5B` hiện có report + backfill executor guarded by dry-run; chưa copy object thật và chưa update database nếu không chạy `--mode backfill --dry-run false`

Nếu không nhắc lại, AI dễ “thiết kế lại” từ đầu sau khi condense.

---

## 4. Luôn có “Decision Log”

Tạo một section hoặc file lưu các quyết định đã khóa:

```md
## Decision Log
- D001: Object key format = public|private/{scope}/{yyyy}/{MM}/{dd}/{guid}{ext}
- D002: Phase 2 chưa thay IFileStorageService cũ
- D003: Audit log action giữ string ở phase đầu
- D004: MediaController hiện đã có workflow mới `upload-url/finalize/download-url/delete`, nhưng business flow cũ vẫn được giữ song song
- D005: Theo plan mới rút gọn, `upload session/finalize`, `signed URL là đường chính`, và `soft delete flow` không phải deliverable của `Phase 1`
- D006: Phase 4 mới đã thêm `MediaFileValidationPolicy` và storage metadata check; file upload không được chỉ tin metadata client gửi lên
- D007: Phase 4 mới audit cả denied access, nhưng replace/approve/reject audit vẫn là follow-up nếu phase sau đụng business workflow đó
- D008: Phase 5A đã tạo inventory tại `docs/AI_Media_Migration_Phase5A_Inventory.md`; phase này không xóa field, không xóa endpoint, không move file thật
- D009: Phase 5B phải bắt đầu bằng dry-run/report cho legacy object migration/backfill trước khi Phase 5C/5D/5E cutover cleanup
- D010: Phase 5B tool nằm ở `server/tools/SmartRentalPlatform.MediaMigrationTool`; mặc định ghi JSON report vào `server/data/media-migration/phase5b-readiness-report.json`
- D011: Phase 5B report mode vẫn read-only; backfill mode mặc định dry-run và chỉ ghi DB khi truyền rõ `--mode backfill --dry-run false`
- D012: Phase 5B backfill yêu cầu schema media-link đã có trên DB target; DB local `localhost:5444` đã reset/apply migration mới nhất và Phase 5B apply ngày 2026-07-13 đã link 3 legacy rows còn thiếu
- D013: Sau Phase 5B apply local, dry-run lại báo `Planned creates = 0`, `Planned links = 0`, `SkippedSchemaNotReady = 0`
- D014: Phase 5C chỉ cắt read path, không xóa compatibility; `RoomingHouseRule` và `ContractOccupantDocument` response scrub private object key khi đã có media id, frontend dùng media URL/id làm đường chính
- D015: Phase 5D chỉ guard/deprecate compatibility; legacy upload/object-key routes vẫn tồn tại, nhưng có `Deprecation` và `X-SRP-Media-*` headers để chuẩn bị cleanup phase sau
- D016: Storage check thật với `--check-storage true` đã xác nhận S3 reachable nhưng toàn bộ 866 legacy references của DB local missing object vật lý; không được tiến hành remove legacy nếu còn cần giữ dữ liệu này
- D017: User xác nhận dữ liệu local/demo không cần giữ, nên Phase 5E ngày 2026-07-13 đã cleanup DB local bằng tool `phase5e --mode cleanup --dry-run false`; kết quả apply 855 deletes, 1 clear, post-check 0 legacy references / 0 storage missing
- D018: Phase 5F ngày 2026-07-13 chặn legacy upload endpoints và admin private object-key route bằng HTTP 410; public object-key route vẫn giữ tạm vì public image read path/listing còn phụ thuộc
- D019: Phase 5G ngày 2026-07-13 là seed/data hygiene, không drop legacy columns; migration `20260713153000_CleanupLegacySampleMediaReferences` chỉ xóa/clear sample refs kiểu `demo/%`, `kfc-scenario/%`, `seed/%`, `/uploads/%` và đã apply trên DB local `localhost:5444`
- D020: Phase 5H ngày 2026-07-13 đã reset DB local, apply full migration chain, chạy seed bằng API startup flags, và xác nhận bằng `phase5b --check-storage true` rằng DB sau seed sạch legacy media refs
```

Khi condense, chỉ cần đưa lại `Decision Log` là giảm mất đồng nhất rất nhiều.

---

## 5. Sau mỗi lần condense, bắt buộc chạy “consistency checklist”

Ngay sau khi condense hoặc mở chat mới, AI phải tự check:

- mục tiêu phase hiện tại là gì
- file nào được sửa
- file nào cấm sửa
- decisions nào đã khóa
- test nào đã pass
- còn migration/fallback nào chưa cleanup

Nếu không trả lời được 6 câu này từ capsule hiện tại, context đã mất quá nhiều chi tiết và phải bổ sung lại.

---

## 6. Chia phase lớn thành sub-phase theo artifact, không theo cảm giác

Không chia kiểu “làm tiếp phần còn lại”.

Phải chia theo artifact rõ ràng:

- schema
- interface
- infrastructure implementation
- adapter compatibility
- business integration
- tests

Ví dụ:

- `Phase 2A`: interfaces + models
- `Phase 2B`: storage + asset service
- `Phase 2C`: tests + cleanup

Nếu một chat phải chạm cả schema, business service, controller và frontend thì gần như chắc chắn context sẽ quá tải.

---

## 7. Mỗi chat phải có “Out of Scope”

Một nguyên nhân lớn gây drift là AI thấy code liên quan và tự sửa thêm.

Mỗi chat luôn phải có:

```text
Out of scope:
- không sửa controller cũ
- không đổi API contract cũ
- không migrate business module
- không remove fallback field
```

Block này giúp AI không lan diff sau khi condense.

---

## 8. Không chỉ lưu “đã làm gì”, phải lưu “tại sao chưa làm”

Sau condense, AI thường biết cái gì đã làm nhưng quên lý do chưa làm phần còn lại.

Vì vậy phase status nên có thêm:

```md
## Deferred intentionally
- compatibility upload cũ vẫn được giữ để không làm gãy frontend/module chưa migrate
- private stream route vẫn được giữ song song với signed download URL
```

Nếu không lưu lý do defer, AI chat sau rất dễ hiểu nhầm đó là thiếu sót vô tình và sửa sai phase.

---

## 9. Dùng “review findings” như guardrail cho phase sau

Sau mỗi phase, lưu ngắn:

```md
## Findings to carry forward
- DefaultMediaPermissionService hiện chỉ phù hợp skeleton, chưa dùng cho admin/contract/KYC thực tế
- private application flow hiện chưa lấy signed URL làm đường chính
- MediaAccessService đang ghi audit mỗi lần open/download-url
```

Những finding này giúp phase sau không vô tình build thêm logic lên trên một nền chưa đủ chín.

Với code hiện tại sau Phase 2, các finding tối thiểu phải carry forward là:

- media API core đã có signed/private download URL và soft delete flow, nhưng nhiều business flow vẫn còn stream backend trực tiếp
- upload session/finalize mới đã tồn tại, nhưng `FilesController` compatibility upload vẫn còn là đường đi chính của nhiều module
- `DefaultMediaPermissionService` không nên bị hiểu là permission matrix cuối cùng cho toàn hệ thống
- semantics của audit failure trong access flow chưa được chốt

Với code hiện tại sau Phase 4 mới, các finding tối thiểu phải carry forward là:

- media core đã enforce file validation theo `MediaScope`, size limit, storage metadata check ở finalize, proxy upload metadata check, private status/permission deny, và audit allow/deny
- denied private access hiện throw `ForbiddenException` và ghi audit action dạng `ViewDenied`/`GenerateDownloadUrlDenied`
- upload/delete đã có audit core; replace/approve/reject chưa phải audit chuẩn toàn hệ thống nếu không được business flow tự ghi
- vẫn còn compatibility/fallback field và một số stream backend flow, nên không được xóa object key/path cũ nếu chưa có cleanup phase riêng

Với code hiện tại sau Phase 5A mới, các finding tối thiểu phải carry forward là:

- `docs/AI_Media_Migration_Phase5A_Inventory.md` là artifact inventory chính cho cutover
- runtime storage chính đã qua `S3StorageService`, nhưng code compatibility/local implementation vẫn còn và chưa được xóa
- legacy field còn ở property/room images, legal document, KYC, contract file, contract occupant docs, meter reading proof, rooming house rule PDF, avatar, và upload compatibility response
- frontend vẫn còn `toAssetUrl`/object-key fallback ở admin, billing, landlord/room detail, contract occupant, appendix, rooming house editor/rule, property image và avatar flows
- chat attachment chưa sẵn sàng cutover vì thiếu conversation-specific permission/design
- VNPT eKYC vẫn cần object key ở provider boundary, không được xóa KYC object-key fields nếu chưa có design mới

Với code hiện tại sau Phase 5B mới, các finding tối thiểu phải carry forward là:

- `LegacyMediaMigrationReadinessService` quét legacy references và match `MediaAsset` bằng normalized object key
- console tool Phase 5B có 2 mode: `report` chỉ đọc và `backfill` mặc định dry-run
- muốn kiểm bucket/storage thật phải chạy tool với `--check-storage true` và AWS config hợp lệ
- backfill executor có thể tạo/link `MediaAsset` theo legacy object key khi schema đã sẵn sàng, nhưng không copy/move object thật
- DB local `localhost:5444` ngày 2026-07-13 đã reset sạch, apply migration mới nhất, chạy Phase 5B apply để tạo/link 3 `MediaAsset` còn thiếu, rồi dry-run lại sạch
- nếu report/backfill dry-run sạch thì phase kế tiếp mới là 5C read path cutover; nếu còn missing storage/object thì cần phase xử lý copy/upload object trước

Sau khi hoàn thành Phase 3, thêm các finding carry forward sau:

- upload compatibility cũ đã tạo `MediaAsset`, nhưng business module cũ vẫn chưa lưu `MediaAssetId`
- object key từ upload compatibility bây giờ theo format mới `public/...`, phase sau không được hardcode assumption theo naming legacy cũ
- legal document upload vẫn đang giữ public compatibility behavior, chưa phải private-media design cuối

Sau khi hoàn thành Phase 4, thêm các finding carry forward sau:

- `ContractFile` main file đã đi qua media layer, nhưng `ContractAppendix` vẫn chưa migrate
- `DefaultMediaPermissionService` hiện đã hiểu `ContractFile` main file private access, nhưng chưa phải permission matrix hoàn chỉnh cho mọi private module
- backfill metadata cho contract file cũ đang có `file_size = 0`
- đường private access đã dùng thật ở phase này là `OpenReadAsync`, chưa phải signed download URL

Sau khi hoàn thành Phase 5, thêm các finding carry forward sau:

- `ContractAppendixService` đã generate appendix raw/masked file qua media core và link `MediaAssetId`
- `DefaultMediaPermissionService` đã cover appendix raw/masked access theo cùng business rule hiện tại
- appendix legacy file cũ vẫn có thể chưa có `MediaAssetId`; phase sau phải quyết định backfill hay giữ fallback dài hạn
- đường private access dùng thật vẫn là `OpenReadAsync`, không được giả định signed download URL đã sẵn sàng

Sau khi hoàn thành Phase 6, thêm các finding carry forward sau:

- `KycVerification` đã có `Front/Back/SelfieMediaAssetId` và KYC upload mới đã tạo `MediaAsset`
- VNPT eKYC integration vẫn dùng `objectKey`, không được tự ý đổi sang stream/token flow nếu chưa thiết kế lại provider boundary
- admin KYC read model đã expose `MediaAssetId`, nhưng admin viewer thật vẫn còn object-key based
- KYC legacy backfill đang dùng `file_size = 0` và content-type suy luận theo extension

---

## 10. Khi nào phải dừng và mở chat mới

Mở chat mới ngay nếu có 1 trong các dấu hiệu:

- phải đọc thêm hơn 7-10 file mới để hiểu tiếp
- AI bắt đầu nhắc sai invariant
- diff chạm hơn 3 subsystem
- AI quên decision log hoặc đề xuất kiến trúc khác với phase trước
- cần giải thích lại lịch sử nhiều hơn là làm việc mới

---

## 11. Prompt template an toàn sau condense

```text
Đây là chat tiếp theo của phase <x>.

Mục tiêu duy nhất:
<...>

Đã chốt trước đó:
- <decision 1>
- <decision 2>

Không được sửa:
- <...>

Out of scope:
- <...>

Findings từ phase trước:
- <...>

Chỉ đọc các file sau:
- <3-7 file>

Yêu cầu:
- giữ invariants
- diff nhỏ
- build pass
- không tự mở rộng scope
```

Nếu tiếp tục ngay từ trạng thái code hiện tại, phase kế tiếp mặc định nên là:

- `Phase 11 - Billing proof image migration`

Phase 10 vừa hoàn thành với các điểm chốt:
- frontend đã có helper `toPublicAssetUrl(imageUrl, objectKey)` cho public property image
- `HouseImageGallery`, `PropertyImageEditor`, `PublicRoomingHouseDetailPage` đã ưu tiên helper public riêng
- client `PropertyImage` và `PropertyImageRequest` đã giữ được `mediaAssetId`
- upload response client type đã hiểu `mediaAssetId`
- helper generic `toAssetUrl` vẫn còn giữ để compatibility cho avatar/legal/pdf/private flows
- rủi ro còn lại: frontend build full repo hiện đang fail ở module `react-pdf` cũ, không phải do Phase 10

trừ khi audit checklist đã được cập nhật rõ ràng sang hướng khác.

---

## 12. Quy tắc cuối cùng

Nếu phải chọn giữa:

- nhét thêm context vào chat hiện tại
- hay mở chat mới với capsule rõ ràng

thì gần như luôn nên chọn:

- `mở chat mới với capsule rõ ràng`

Với migration nhiều phase, `context continuity` quan trọng hơn `context length`.
