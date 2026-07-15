# Tong hop loi sau media manual test

## Pham vi

- Branch: `feat/local-to-cloud-storage`
- Commit duoc test: `3d99973`
- Ngay test: `2026-07-15`
- Nguon bang chung chi tiet: `docs/Media_Manual_Test_Checklist.md`
- Quy uoc phan loai:
  - `MEDIA`: loi nam trong upload, mapping, permission, render hoac lifecycle cua media asset.
  - `NON_MEDIA`: loi UI/React hoac nghiep vu chung, khong phat sinh tu media schema/storage.
  - `OBSERVATION`: canh bao hoac gioi han moi truong test, chua du bang chung de ket luan la loi san pham.

## Tong quan

| ID | Phan loai | Muc do | Trang thai | Tom tat |
| --- | --- | --- | --- | --- |
| MT-001 | NON_MEDIA | P1 | OPEN | Search render `<button>` long trong `<button>`. |
| MT-002 | MEDIA | P1 | FIXED - VERIFIED | Backend chan anh khu tro/phong thu 11. |
| MT-003 | NON_MEDIA | P1 | OPEN | `PageHeader` dat React node dang `<div>` vao trong `<p>`. |
| MT-004 | MEDIA | P1 | OPEN | KYC status khong tra va khong render lai ba anh private. |
| MT-005 | MEDIA | P1 | FIXED - VERIFIED | Direct chat map avatar tu `AvatarMediaAssetId`. |
| MT-006 | MEDIA | P1 | OPEN | Khong the go avatar conversation bang `avatarMediaAssetId: null`. |
| MT-007 | MEDIA | P1 | FIXED - VERIFIED | Landlord invoice detail render meter proof private. |
| MT-008 | MEDIA | P1 | OPEN | Soft delete chi xoa logic trong DB, khong xoa object S3. |
| MT-009 | MEDIA | P1 | OPEN | Upload session het han khong duoc cleanup. |

Tong cong: `7 MEDIA`, `2 NON_MEDIA`.

## Loi do media

### MT-002 - Backend chap nhan hon 10 anh khu tro

- Trang thai: `FIXED - VERIFIED`.
- Xu ly: them upper bound 10 anh trong `RoomingHouseMediaService` va `RoomValidationRules`; them regression test cho payload 11 anh.
- Xac minh: unit test lien quan PASS; API thuc te tra `400 VALIDATION_ERROR`, `details.max: 10` truoc khi kiem tra media asset.

- Hien tuong: `PUT /api/rooming-houses/{id}/images` chap nhan payload 11 anh voi HTTP `200`.
- Nguyen nhan: client co gioi han hien thi/thao tac, nhung backend chi validate toi thieu 3 anh, mot cover va `mediaAssetId`; khong co upper bound 10 anh.
- Bang chung code: `RoomingHouseMediaService.ValidatePropertyImages` tai `server/src/SmartRentalPlatform.Application/RoomingHouses/RoomingHouseMediaService.cs:308`.
- Anh huong: co the bypass client, tao payload qua lon va lam UI/API khong con dung quy uoc toi da 10 anh.
- Huong xu ly: dat chung hang so max image o server, validate truoc khi thay reference va them regression test payload 10/11 anh.

### MT-004 - KYC owner khong xem lai duoc anh sau reload

- Hien tuong: owner van mo truc tiep private media voi HTTP `200`, nhung `/me/kyc/status` va `KycStatusPage` khong hien mat truoc, mat sau, selfie.
- Nguyen nhan: `KycStatusResponse` khong co media asset ID/view URL; `MapStatus` chi map status/OCR va UI cung khong co khoi render `PrivateMediaImage`.
- Bang chung code:
  - `server/src/SmartRentalPlatform.Contracts/Kyc/Responses/KycStatusResponse.cs:3`
  - `server/src/SmartRentalPlatform.Application/Kyc/KycService.cs:325`
  - `client/src/features/kyc/pages/KycStatusPage.tsx:110`
- Anh huong: upload va permission dung, nhung read model sau migration bi thieu nen flow owner khong hoan chinh.
- Huong xu ly: bo sung ba media ID/URL vao response, map tu `KycVerification` va render bang authenticated private-media component.

### MT-005 - Direct chat bo qua avatar media moi cua user

- Trang thai: `FIXED - VERIFIED`.
- Xu ly: `ChatService.MapParticipant` uu tien public media path tu `AvatarMediaAssetId`, chi fallback URL legacy khi chua co media asset.
- Xac minh: regression test PASS; API conversation tra `/api/media/public/18dc806a-580a-448a-bf0b-b11660921519` cho participant thay vi `null`/legacy.

- Hien tuong: profile/header hien avatar moi, nhung direct chat hien fallback chu cai.
- Nguyen nhan: `ChatService.MapParticipant` chi tra `participant.User.AvatarUrl` legacy, khong build URL tu `AvatarMediaAssetId`.
- Bang chung code: `server/src/SmartRentalPlatform.Application/Chat/ChatService.cs:890`.
- Anh huong: cung mot user co avatar khac nhau giua profile va chat; migration sang media chua cutover het read path.
- Huong xu ly: map avatar media theo cung quy tac cua profile/conversation va them test participant co `AvatarMediaAssetId` nhung `AvatarUrl` null.

### MT-006 - Khong the xoa avatar conversation

- Hien tuong: PATCH voi `avatarMediaAssetId: null` tra thanh cong nhung avatar cu van con; UI khong co thao tac xoa.
- Nguyen nhan: service chi vao nhanh media khi `AvatarMediaAssetId.HasValue`; explicit null bi xem nhu field khong duoc gui. Request contract khong phan biet `omitted` va `explicit null`.
- Bang chung code: `server/src/SmartRentalPlatform.Application/Chat/ChatService.cs:189`.
- Anh huong: thay avatar duoc, nhung khong the go avatar va retire asset cu.
- Huong xu ly: them co `clearAvatar` hoac optional-field contract co presence semantics; khi clear phai null reference va retire asset cu trong cung transaction.

### MT-007 - Landlord invoice detail khong render meter proof

- Trang thai: `FIXED - VERIFIED`.
- Xu ly: them nut xem proof va lightbox dung `PrivateMediaImage` trong `InvoiceDetailSection` cua landlord.
- Xac minh: client build PASS; browser hien nut `Xem chi so dien`, modal tai xong blob private media kich thuoc `1321 x 1708`.

- Hien tuong: API landlord tra du `meterReadingProofMediaAssetId` va `meterReadingProofImageUrl`; tenant xem duoc anh, landlord chi thay dong `Dien (25 kWh)`.
- Nguyen nhan: `InvoiceDetailSection` cua landlord chi render item type, description, quantity, price va amount; khong dung proof URL. Tenant page da co nut va `PrivateMediaImage`.
- Bang chung code:
  - Thieu render: `client/src/features/billing/pages/LandlordBillingPage.tsx:1438`
  - Implementation dung o tenant: `client/src/features/billing/pages/TenantInvoicesPage.tsx:257`
- Anh huong: du lieu va permission dung nhung landlord khong the doi chieu anh cong to tu hoa don da tao.
- Huong xu ly: dung lai lightbox/private-media pattern cua tenant trong landlord invoice detail.

### MT-008 - Soft delete khong xoa object S3

- Hien tuong: sau `DELETE /api/media/{id}`, API media chan noi dung nhung signed S3 URL cap truoc delete van tra HTTP `200`.
- Nguyen nhan: `MediaWorkflowService.SoftDeleteAsync` chi chuyen status sang `Deleted` va set `DeletedAt`; khong goi `_mediaStorageService.DeleteAsync` va khong co worker xoa object sau do.
- Bang chung code: `server/src/SmartRentalPlatform.Infrastructure/Media/MediaWorkflowService.cs:163`.
- Anh huong: object cu ton tai tren S3, signed URL cu con doc duoc den khi het han, tang chi phi storage va tao rui ro retention/private-data.
- Huong xu ly: chon ro policy:
  - Hard-delete object ngay va chi commit DB khi storage delete thanh cong; hoac
  - Outbox/background purge co retry, trang thai `PendingDeletion`, audit va retention window ro rang.
- Bat buoc test: sau delete, API route va signed URL cu deu khong tra noi dung; object khong con tren storage.

### MT-009 - Khong cleanup upload session het han

- Hien tuong: DB con 8 asset `PendingUpload` tao luc `11:50-12:02 UTC`, qua TTL 15 phut nhieu gio.
- Nguyen nhan: workflow tao row `PendingUpload` va signed URL TTL 15 phut, nhung khong co hosted service/job tim va cleanup session het han.
- Bang chung code:
  - TTL va tao pending asset: `server/src/SmartRentalPlatform.Infrastructure/Media/MediaWorkflowService.cs:15`
  - Khong tim thay media cleanup worker trong `server/src`.
- Anh huong: row orphan tang dan; neu binary da len S3 nhung finalize that bai thi co the tao ca object orphan.
- Huong xu ly: background cleanup theo batch cho `PendingUpload` qua TTL + grace period; xoa object neu ton tai, mark `Deleted/Expired`, ghi audit va co metric.

## Loi khong do media

### MT-001 - Nested button tren trang search

- Hien tuong: React bao `<button>` khong duoc long trong `<button>`; co nguy co click/hydration khong on dinh.
- Nguyen nhan: toan bo `search-result-card` la button, ben trong lai render `FavoriteButton`, cung la button.
- Bang chung code:
  - Card ngoai: `client/src/features/rooming-houses/SearchRoomingHousesPage.tsx:1314`
  - Favorite button trong: `client/src/features/rooming-houses/components/FavoriteButton.tsx:35`
- Tai sao khong phai media: image URL va media render van dung; loi nam o cau truc HTML/interaction cua search card.
- Huong xu ly: doi card ngoai thanh link/container co keyboard handling hoac tach favorite button ra ngoai interactive parent.

### MT-003 - Invalid DOM trong PageHeader

- Hien tuong: React canh bao DOM/hydration tren landlord rooming-house va room detail.
- Nguyen nhan: `PageHeader` khai bao `eyebrow` la `ReactNode` nhung luon boc trong `<p>`; cac page truyen vao mot `<div>` co icon va text.
- Bang chung code:
  - Wrapper `<p>`: `client/src/shared/components/ui/PageHeader.tsx:34`
  - Caller truyen `<div>`: `client/src/features/landlord/pages/RoomingHouseDetailPage.tsx:515` va `RoomDetailPage.tsx:533`.
- Tai sao khong phai media: loi semantic HTML cua shared layout, xuat hien bat ke tab anh co duoc mo hay khong.
- Huong xu ly: doi wrapper eyebrow thanh `<div>` hoac gioi han prop thanh text/inline node.

## Canh bao va gioi han khong tinh la loi media

### NU1903

- Loai: dependency security warning, khong phai compile/test failure va khong do media logic.
- Nguon: test project tham chieu `Microsoft.EntityFrameworkCore.Sqlite 10.0.8`, keo transitive `SQLitePCLRaw.lib.e_sqlite3 2.1.11` co advisory muc cao.
- Bang chung: `server/tests/SmartRentalPlatform.UnitTests/SmartRentalPlatform.UnitTests.csproj:16`.
- Xu ly rieng: nang package transitive/direct pin len ban da fix va chay lai restore/test.

### React duplicate key `many-rooms` va `affordable`

- Loai: NON_MEDIA observation tren home; khong lien quan media asset.
- Trang thai: console da tai hien canh bao duplicate key, nhung chua co trace du de chot nguyen nhan goc trong run media.
- Huong xu ly: tach thanh bug UI rieng va kiem tra noi tao danh sach category/section truoc khi render.

### SignalR disconnect/reconnect

- Loai: environment/realtime observation, khong phai media.
- Trang thai: log cu ghi WebSocket `1006` va negotiation `Failed to fetch` trong luc server restart/test; chua co bang chung lam mat message hoac attachment.
- Huong xu ly: chi mo bug rieng neu tai hien trong mot phien server on dinh.

### S3 CORS preflight 403

- Loai: MEDIA environment behavior, nhung hien tai khong phai functional failure vi client fallback sang backend upload thanh cong (`204`) va finalize duoc.
- Huong xu ly: co the cau hinh bucket CORS de upload truc tiep; neu chu dich giu fallback thi can monitor tan suat fallback.

### Cac muc PARTIAL do browser automation

- Multi-select file dialog, drag-and-drop, cropper va offline + file chooser chua duoc thao tac hoan toan tu dong.
- Day la gioi han bang chung manual test, khong duoc tinh thanh loi san pham.
- Cac flow API/backend va render sau reload lien quan van duoc test rieng.

## Thu tu xu ly de on dinh PR

1. `MT-008`, `MT-009`: storage lifecycle va cleanup, rui ro data retention/orphan.
2. `MT-004`, `MT-006`: hoan tat KYC read path va explicit clear group avatar sau media cutover.
3. `MT-001`, `MT-003`: tach khoi media neu muon giu PR gon, nhung van can sua truoc khi release UI.
4. `NU1903`, duplicate key va SignalR: theo doi/tach issue rieng, khong tron vao root cause cua media migration.
