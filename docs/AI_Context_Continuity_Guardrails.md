# AI Context Continuity Guardrails

## Mục tiêu

File này dùng để giảm 2 rủi ro khi làm nhiều phase với AI:

- `context window bị tràn`
- `sau khi condense, AI mất chi tiết quan trọng và bắt đầu lệch khỏi design ban đầu`

Tài liệu này đi kèm với `docs/AI_Media_Migration_Phase_Plan.md`.

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

Nếu không nhắc lại, AI dễ “thiết kế lại” từ đầu sau khi condense.

---

## 4. Luôn có “Decision Log”

Tạo một section hoặc file lưu các quyết định đã khóa:

```md
## Decision Log
- D001: Object key format = public|private/{scope}/{yyyy}/{MM}/{dd}/{guid}{ext}
- D002: Phase 2 chưa thay IFileStorageService cũ
- D003: Audit log action giữ string ở phase đầu
- D004: Ở thời điểm Phase 2, private download URL end-to-end chưa được đưa vào application flow
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
- Ở thời điểm Phase 2, private signed URL chưa được đưa vào application flow vì phase này chỉ dựng foundation
- Business permission matrix chưa làm ở Phase 2 vì chỉ đang dựng skeleton
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

- `MediaAccessService.GetDownloadUrlAsync` chưa là đường chính của private application flow
- `DefaultMediaPermissionService` mới chỉ phù hợp skeleton owner-based
- signed URL thật hiện đã có ở `S3StorageService`, nhưng chưa được business flow dùng làm path chính
- semantics của audit failure trong access flow chưa được chốt

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
