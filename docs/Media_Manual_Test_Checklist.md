# Media Manual Test Checklist

## Quy uoc

- `[ ]`: Chua test hoac can test lai.
- `[x]`: Da test PASS tren branch/commit ghi trong run log.
- Neu FAIL, giu `[ ]` va ghi `FAIL:` cung loi tai ngay dong test.
- Moi test phai co it nhat mot bang chung: browser, HTTP response, database query hoac log.
- Test theo tung cum; khong chuyen cum khi con loi P0 trong cum hien tai.

## Moi truong test

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:5294`
- Database: PostgreSQL container `smart_rental_postgres`
- Storage: AWS S3 bucket development
- Rooming house chinh: `20000000-0000-0000-0000-000000000001`
- Landlord: `landlord.demo@example.com`

## Cum 1 - Smoke, migration, seed va public media

- [x] Server khoi dong va API login phan hoi thanh cong.
- [x] Client mo duoc `/search` khong bi trang trang.
- [x] Khong co pending EF migration.
- [x] Dev seed co dung 50 khu tro lon theo cau hinh hien tai.
- [x] Moi `property_images` dang hoat dong deu co `media_asset_id`.
- [x] Khong con property image chi dua vao `image_url` legacy.
- [x] Search/listing lay cover tu media asset va anh tra `200 image/*`.
- [x] Public rooming-house detail render du anh tu media asset.
- [x] Anonymous mo duoc public rooming-house image.
- [x] Anonymous mo duoc house-rule PDF voi `200 application/pdf`.
- [x] Browser Console/Network khong co loi media `500` hoac broken image.
- [ ] Search khong co React DOM/hydration error. FAIL `MT-001`: `search-result-card` button chua `FavoriteButton` button.

## Cum 2 - Anh khu tro

- [x] Chon anh PNG va upload thanh cong khi signed S3 upload bi CORS chan.
- [x] Backend fallback upload tra `204`, finalize va link media thanh cong.
- [x] Reload trang quan ly va anh moi van render tu `/api/media/public/{id}`.
- [ ] Upload dong thoi nhieu anh JPG/PNG/WebP. PARTIAL: 3 backend upload chay song song `204`, public render dung MIME; chua thao tac multi-select qua file dialog.
- [x] Luu khu tro voi toi thieu 3 anh.
- [x] Dat anh moi lam cover, reload va cover van dung.
- [ ] Keo doi thu tu anh, luu va reload van dung thu tu. PARTIAL: API save/reload dung thu tu; chua thao tac drag card qua browser automation.
- [x] Xoa mot anh khong phai cover va reload khong con anh do.
- [x] Xoa cover thi mot anh con lai duoc chon lam cover.
- [x] Chan anh sai MIME, sai extension va file vuot 10 MB.
- [x] Chan anh thu 11 khi khu tro da co 10 anh. FIXED `MT-002`: API tra `400 VALIDATION_ERROR`, `details.max: 10`; regression test bao phu ca khu tro va phong.
- [x] Landlord khac khong the sua anh khu tro nay.

## Cum 3 - Anh phong

- [ ] Upload mot va nhieu anh phong. PARTIAL: mot JPG va bo PNG/WebP upload/finalize thanh cong; chua thao tac multi-select qua file dialog he dieu hanh.
- [x] Luu anh phong, reload va tat ca anh van render.
- [ ] Dat cover va doi thu tu anh phong. PARTIAL: API save va browser reload dung cover/thu tu; chua thao tac drag card that qua browser automation.
- [x] Xoa anh phong va kiem tra media cu khong con duoc tham chieu.
- [x] Public room detail render cover va gallery tu media asset.
- [x] Landlord khac khong the sua anh phong. API tra `404` de che giau phong khong thuoc landlord.

## Cum 4 - KYC va giay to phap ly

- [x] Tenant upload mat truoc, mat sau va selfie; ca ba asset finalize `Uploaded` dung scope `KycDocument`.
- [x] Submit KYC happy path thanh cong voi mock eKYC; ket qua `PendingAdminReview`, `Passed`, risk `Low`.
- [x] Reload trang KYC va ba anh private van render cho chu so huu. `MT-004` FIXED - VERIFIED; ca ba anh tai qua authenticated blob va giu nguyen sau full-page reload.
- [x] Admin/reviewer mo duoc du ba anh KYC tren UI; PNG/JPEG/WebP deu render dung.
- [x] Anonymous va user khac bi `401/403` khi mo anh KYC; chu so huu va admin nhan `200`.
- [x] Thay anh KYC cu; candidate cu khong co KYC reference va da duoc cleanup sang `Deleted`.
- [x] Landlord upload hai anh phap ly khu tro va luu thanh cong; lien ket dung media schema moi.
- [ ] Chu khu tro xem duoc anh phap ly sau reload. PARTIAL: owner API/detail va hai private media deu `200`; UI editor cua draft bi chan vi seed landlord chua hoan tat ho so.
- [x] Landlord khac va anonymous khong xem duoc anh phap ly; private media tra `403/401`, house detail tra `403/401`.

## Cum 5 - Luat khu tro

- [ ] Landlord upload house-rule PDF va luu thanh cong. PARTIAL: authenticated media workflow upload `204`, finalize/link va UI save toast thanh cong; in-app browser khong ho tro automation file chooser.
- [x] Reload trang quan ly va PDF van mo/render duoc; UI tro dung public media asset hien tai.
- [x] Tenant/anonymous mo duoc house rule khong can quyen private; ca hai tra `200 application/pdf` dung kich thuoc.
- [x] Thay PDF cu bang PDF moi; UI va public detail dung asset moi, hai public route cu tra `404` va asset cu o `Deleted`.
- [x] Chan file house rule khong phai PDF hoac vuot 20 MB; ca hai tra `400 VALIDATION_ERROR` va khong tao media asset.

## Cum 6 - Avatar user va avatar nhom chat

- [ ] Upload avatar user moi va reload trang. PARTIAL: authenticated media workflow upload `204`, profile update va reload thanh cong; in-app browser khong ho tro automation file chooser/cropper.
- [x] Avatar moi render o profile, header va chat. FIXED `MT-005`: direct conversation API tra public media path tu `AvatarMediaAssetId`; regression test PASS.
- [x] Avatar cu khong con duoc tham chieu sau khi thay; asset cu `Deleted`, 0 user reference va public route tra `404`.
- [x] Anonymous mo duoc avatar public voi `200 image/jpeg`.
- [ ] Upload/thay avatar conversation va reload chat. PARTIAL: `/api/chat/avatars` va PATCH conversation thanh cong, replacement dung; in-app browser khong automation duoc file chooser.
- [x] Avatar conversation render trong list va chat header; ca hai dung cung public asset moi 256px sau reload.
- [x] Xoa avatar conversation khong lam hong conversation. `MT-006` FIXED - VERIFIED; list/header/panel doi sang placeholder, asset cu `Deleted` va avatar khong quay lai sau reload.

## Cum 7 - Chat attachment

- [ ] Gui tin nhan kem anh; nguoi gui va nguoi nhan deu xem duoc. PARTIAL: upload/send API va UI hai phia render anh private 256px; in-app browser khong automation duoc file chooser.
- [ ] Gui PDF/file; nguoi nhan tai duoc dung ten va Content-Type. PARTIAL: upload/send API va UI card dung ten; download `200 application/pdf`, dung `Content-Disposition`; chua thao tac file chooser.
- [x] Reload chat va attachment van con; tenant va landlord deu render anh/PDF sau reload.
- [x] Anonymous bi `401` khi mo private attachment anh va file.
- [x] User ngoai conversation bi `403` khi mo private attachment anh va file.
- [x] Khong the link cung media asset vao hai message; request thu hai tra `400 CHAT_MEDIA_ALREADY_LINKED` va khong tao message trung.
- [x] Notification loi khong lam mat message/attachment da gui; trigger loi tam thoi giu notification count `4 -> 4`, API van tra message va UI reload van render anh.
- [x] Xoa message attachment khong gay loi FK hoac loi UI; message soft-delete, media `Deleted`, 0 reference va UI con conversation/PDF/anh khac.

## Cum 8 - Hop dong, phu luc, hoa don va cong to

- [x] Landlord va tenant trong hop dong mo duoc contract PDF; ca hai nhan `200 application/pdf` va browser tenant mo duoc ban xem truoc 2 trang.
- [x] User ngoai hop dong khong mo duoc contract PDF; outsider bi `403`, anonymous bi `401`.
- [x] Signed URL het han khong con truy cap duoc; URL S3 cu sau TTL 5 phut tra `403`.
- [x] Tao phu luc va mo lai PDF phu luc; phu luc `PL-001-KFC-101-20260415` render du 2 trang tren browser.
- [x] Upload giay to nguoi o cung va render lai duoc; sau reload/edit phu luc, anh mat truoc tai thanh blob private media dung asset.
- [x] Trang hoa don landlord/tenant render dung private media lien quan. FIXED `MT-007`: landlord co nut `Xem chi so dien`; modal tai blob private media `1321 x 1708`, tenant van PASS.
- [x] Upload anh cong to va tao meter reading thanh cong; asset duoc link vao meter reading, chi so `100 -> 125`, consumption `25`.
- [x] Reload meter reading va anh proof van render; tenant lightbox tai lai blob private media, landlord/tenant deu mo truc tiep asset voi `200`.

## Cum 9 - Failure, permission va cleanup

- [ ] Mat mang giua upload hien loi ro rang va cho phep retry. PARTIAL: client co thong bao `Khong the tai tep len storage.` va cho phep chon/upload lai; da ep backend failure thanh cong, nhung chua thao tac offline + file chooser tren browser.
- [x] Signed S3 upload that bai thi fallback backend hoat dong; preflight tu origin client tra `403`, backend fallback tra `204` va finalize thanh `Uploaded`.
- [x] Backend upload that bai thi khong finalize media asset; binary sai size tra `400`, finalize tiep tuc tra `400`, asset van `PendingUpload` truoc khi cleanup test.
- [x] Tenant khong the sua/xoa media cua landlord; PUT bi `401`, finalize/delete bi `403`.
- [x] Landlord A khong the sua/xoa media cua landlord B; PUT bi `401`, finalize/delete bi `403`.
- [ ] Media da xoa khong tra lai noi dung cu. FAIL `MT-008`: API media da chan sau delete, nhung signed S3 URL cap truoc delete van tra `200` va noi dung cu.
- [ ] Khong con object/media orphan sau cac flow thay va xoa file. FAIL `MT-008`, `MT-009`: soft delete khong xoa object S3 va DB con 8 phien `PendingUpload` qua TTL 15 phut.
- [x] Browser Console/Network khong co media `500`, CORS blocker hoac broken image ngoai case co chu dich; public detail co 0 broken image va 0 media error/warning.

## Regression tu dong bat buoc sau khi manual test

- [ ] Client Vitest PASS.
- [ ] Client production build PASS.
- [ ] Server unit tests PASS.
- [ ] Server integration tests PASS.
- [ ] `git diff --check` PASS.

## Run log

| Run | Ngay | Branch / Commit | Cum | Ket qua | Ghi chu |
| --- | --- | --- | --- | --- | --- |
| 1 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 2 mot anh khu tro | PASS | S3 CORS preflight 403; backend fallback 204; public render 200 image/png. |
| 2 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 1 | PARTIAL | 11 media/public checks PASS; phat hien `MT-001` tren search. |
| 3 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 2 | PARTIAL | Cover/delete/validation/permission PASS; multi-select va drag con manual; phat hien `MT-002`, `MT-003`. |
| 4 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 3 | PARTIAL | Save/reload/delete/public gallery/permission PASS; multi-select va drag con manual; `MT-003` cung xuat hien tren room detail. |
| 5 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 4 | PARTIAL | 7/9 PASS; KYC owner UI FAIL `MT-004`; legal owner UI bi chan boi du lieu seed, backend va permission matrix PASS. |
| 6 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 5 | PARTIAL | 4/5 PASS; replacement/public access/validation PASS; file chooser UI khong automation duoc, khong co finding media moi. |
| 7 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 6 | PARTIAL | 3/7 PASS, 2 PARTIAL; phat hien chat bo qua user media avatar (`MT-005`) va khong the xoa group avatar (`MT-006`). |
| 8 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 7 | PARTIAL | 6/8 PASS, 2 PARTIAL do file chooser; permission/reuse/notification failure/delete cleanup PASS, khong co finding attachment moi. |
| 9 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 8 | PARTIAL | 7/8 PASS; contract PDF, signed URL expiry, appendix PDF, occupant document va meter proof PASS; phat hien landlord invoice detail khong render meter proof (`MT-007`). |
| 10 | 2026-07-15 | `feat/local-to-cloud-storage` / `3d99973` | Cum 9 | PARTIAL | 5/8 PASS, 1 PARTIAL; failure/fallback/permission/browser PASS; targeted client/server media tests 10/10 PASS; phat hien soft delete khong xoa object S3 (`MT-008`) va 8 upload session het han khong duoc cleanup (`MT-009`). |
| 11 | 2026-07-15 | `feat/local-to-cloud-storage` / working tree | Quick media fixes | PASS | `MT-002`, `MT-005`, `MT-007` da sua; API/browser verify PASS, targeted server tests 24/24 va client build PASS. |

## Findings

| ID | Muc do | Trang/flow | Trang thai | Mo ta |
| --- | --- | --- | --- | --- |
| MT-001 | P1 | `/search` | OPEN | React bao `<button>` khong duoc long trong `<button>`: card ket qua dang boc `FavoriteButton`; co nguy co hydration va click khong on dinh. |
| MT-002 | P1 | `PUT /api/rooming-houses/{id}/images` | FIXED - VERIFIED | Backend enforce toi da 10 anh cho khu tro/phong; payload 11 anh tra `400 VALIDATION_ERROR`, `max: 10`. |
| MT-003 | P1 | Landlord rooming-house/room detail | OPEN | `PageHeader` render `eyebrow` trong `<p>` nhung `RoomingHouseDetailPage` va `RoomDetailPage` truyen `<div>`, gay React DOM/hydration error. |
| MT-004 | P1 | `/me/kyc/status` | FIXED - VERIFIED | Status tra du ba media asset ID; browser reload render du ba anh private blob `720x460`; KYC tests, Vitest va client build PASS. |
| MT-005 | P1 | `/messages` direct chat | FIXED - VERIFIED | `ChatService.MapParticipant` uu tien public path tu `AvatarMediaAssetId`; API conversation va regression test deu PASS. |
| MT-006 | P1 | `/messages` group avatar | FIXED - VERIFIED | UI go avatar thanh cong; conversation null reference, asset cu `Deleted`; list/header/panel giu placeholder sau reload; automated tests va build PASS. |
| MT-007 | P1 | `/landlord/invoices/{id}` | FIXED - VERIFIED | Landlord detail render nut proof va lightbox `PrivateMediaImage`; browser xac nhan blob private media tai xong. |
| MT-008 | P1 | `DELETE /api/media/{id}` | OPEN | `SoftDeleteAsync` chi doi status DB sang `Deleted`, khong goi storage delete. API chan asset sau delete nhung signed S3 URL da cap van tra `200`, nen object cu con ton tai va truy cap duoc den khi URL het han. |
| MT-009 | P1 | Media upload lifecycle | OPEN | Khong co cleanup cho upload session het han; DB con 8 asset `PendingUpload` tao luc 11:50-12:02 UTC, da qua TTL 15 phut nhieu gio nhung chua bi danh dau/xoa. |
