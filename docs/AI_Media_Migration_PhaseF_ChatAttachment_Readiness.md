# AI Media Migration Phase F - Chat Attachment Readiness

## Mục tiêu

Chuẩn bị đường cắm kỹ thuật cho chat attachment trên media core mà không giả lập conversation persistence hoặc permission matrix chưa tồn tại.

## Trạng thái trước phase này

- `MediaScope.ChatAttachment` đã tồn tại ở enum media scope
- Phase 14 mới dừng ở mức design boundary
- chat hiện tại chỉ có AI conversation cache:
  - không có `Conversation` persisted
  - không có `ConversationMessage` persisted
  - không có participant ownership rule trong DB

## Scope phase này

- mở `FileUploadScope.ChatAttachment`
- map upload scope đó sang private `MediaAsset`
- thêm helper client generic cho private media để team chat dùng lại
- cập nhật docs/audit để phản ánh đúng trạng thái readiness

## Không làm trong phase này

- không tạo schema chat mới
- không dùng `conversationId` cache hiện tại làm khóa ownership cho media
- không thêm permission heuristic kiểu “nếu cùng rooming house thì xem được”
- không sửa `RoomingHouseAiChatService` để nhét attachment giả

## Quyết định đã khóa

- `ChatAttachment` upload phải tạo `MediaVisibility.Private`
- helper client cho private media phải generic, không gắn cứng vào contract module
- participant access của chat attachment chỉ được mở khi có conversation/message persistence thật

## Thay đổi implementation

- backend:
  - thêm `FileUploadScope.ChatAttachment`
  - `FilesController.UploadPdf` chấp nhận scope `ChatAttachment`
  - `MediaBackedFileStorageService` map `ChatAttachment -> MediaScope.ChatAttachment`
  - `MediaBackedFileStorageService` map `ChatAttachment -> MediaVisibility.Private`
- frontend:
  - `client/src/features/files/api.ts` nhận scope `ChatAttachment`
  - `client/src/shared/api/endpoints.ts` có `MEDIA.PRIVATE_BY_ID`, `MEDIA.PRIVATE_DOWNLOAD`, `MEDIA.PRIVATE_DOWNLOAD_URL`
  - `client/src/shared/api/media.ts` thêm helper lấy private download URL và build route private media

## Kết quả sau phase này

- uploader chung của hệ thống đã có thể tạo private media asset cho chat attachment
- response upload đã trả `mediaAssetId` để module chat lưu lại sau này
- team chat có helper chung để mở/download private media khi message attachment contract thật xuất hiện

## Điều vẫn còn thiếu để attachment hoàn chỉnh

1. `Conversation` persisted trong DB
2. `ConversationMessage` persisted trong DB
3. participant rule rõ ràng cho landlord/tenant/occupant
4. entity hoặc mapping table link `message -> mediaAssetId`
5. `DefaultMediaPermissionService` hoặc permission service mở được access theo conversation participant
6. request/response contract thật cho message attachment ở module chat

## Gate để chuyển sang implementation thật

- Người 5 đã mở conversation model persisted
- permission matrix của chat được chốt
- đã quyết định attachment sống ở message entity nào
- có test allow/deny cho participant và outsider
