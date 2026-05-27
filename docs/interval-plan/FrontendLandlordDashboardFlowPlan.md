# Implementation Plan — Frontend Landlord Flows

## 1. Mục tiêu

Hoàn thiện các flow frontend chính cho Smart Rental Platform:

- Home theo role.
- Đăng ký làm chủ trọ bằng form 4 tab hiện có.
- Kênh chủ trọ sau khi được admin duyệt.
- Dashboard quản lý khu trọ.
- Quản lý phòng trong khu trọ đã duyệt.

Phạm vi hiện tại: frontend React + Vite + TypeScript. Backend API đã có, chỉ cần confirm endpoint ở một vài điểm chưa rõ.

---

## 2. Trạng thái hiện tại

### Đã có

- `LandlordRegisterPage.tsx`
  - Form 4 tab đăng ký khu trọ đầu tiên.
  - Có draft, update basic info, amenities, images, legal document, submit.
- `landlordApi.ts`
  - Đã có API onboarding và rooming house registration.
- `landlord.types.ts`
  - Đã có types onboarding, rooming house detail, `canEdit`, `canSubmit`, `canEnterLandlordDashboard`.
- `endpoints.ts`
  - Đã có nhiều endpoint `ROOMING_HOUSES`, `ROOMS`, `ADMIN`.
- `OnboardingGuard.tsx`
  - Đã điều hướng auth/KYC cơ bản.
- Admin approval page
  - Đã có duyệt KYC và duyệt khu trọ.

### Cần làm tiếp

- Làm lại `MePage.tsx` thành Home role-based.
- Thêm route `/landlord/dashboard`.
- Tạo `LandlordDashboardPage`.
- Tạo `RoomingHouseDetailPage`.
- Tạo UI quản lý phòng.
- Thêm `roomApi.ts`.
- Bổ sung types room.
- Điều chỉnh `LandlordRegisterPage` khi user đã là Landlord.

---

## 3. Flow tổng thể

```text
Login/Register
  ↓
Verify email
  ↓
Home /me
  ├── Tenant chưa KYC → /me/kyc
  ├── Tenant đã KYC → có nút "Đăng ký làm chủ trọ"
  ├── Landlord → có nút "Kênh chủ trọ"
  └── Admin → có nút "Duyệt hồ sơ"
```

Flow landlord:

```text
/me
  ↓
Đăng ký làm chủ trọ
  ↓
/landlord/register
  ↓
Submit khu trọ đầu tiên
  ↓
Admin duyệt
  ↓
User có role Landlord
  ↓
/landlord/dashboard
  ↓
Quản lý khu trọ / phòng
```

---

## 4. Step 1 — Refactor Home Page

### File

```text
client/src/features/me/pages/MePage.tsx
```

### Mục tiêu

Home phải hiển thị button theo role.

### Logic

```text
Nếu currentUser.roles includes Admin
  → Hiển thị "Duyệt hồ sơ"

Nếu currentUser.roles includes Landlord
  → Hiển thị "Kênh chủ trọ"

Nếu chưa có Landlord
  → Hiển thị "Đăng ký làm chủ trọ"
```

### Button behavior

```text
"Duyệt hồ sơ" → /admin/approvals
"Kênh chủ trọ" → /landlord/dashboard
"Đăng ký làm chủ trọ"
  → gọi checkLandlordEligibility()
  → nếu KYC approved → /landlord/register
  → nếu chưa KYC → /me/kyc
```

### Checklist

- [ ] Đọc `currentUser.roles`.
- [ ] Hiển thị đúng button theo role.
- [ ] Thêm loading state khi check landlord eligibility.
- [ ] Thêm error state nếu API eligibility lỗi.
- [ ] Navigate đúng route.

---

## 5. Step 2 — Update Route Constants

### File

```text
client/src/app/router/routePaths.ts
```

### Thêm

```ts
LANDLORD: {
  REGISTER: '/landlord/register',
  DASHBOARD: '/landlord/dashboard',
  ROOMING_HOUSE_DETAIL: (id: string) => `/landlord/rooming-houses/${id}`
}
```

### Checklist

- [ ] Thêm `DASHBOARD`.
- [ ] Thêm `ROOMING_HOUSE_DETAIL`.
- [ ] Không phá route `REGISTER` hiện có.

---

## 6. Step 3 — Update Routes

### File

```text
client/src/app/router/routes.tsx
```

### Thêm route

```text
/landlord/dashboard
  → LandlordDashboardPage

/landlord/rooming-houses/:id
  → RoomingHouseDetailPage
```

Tạm thời các route này nằm trong `ProtectedRoute` + `OnboardingGuard`.

### Checklist

- [ ] Import `LandlordDashboardPage`.
- [ ] Import `RoomingHouseDetailPage`.
- [ ] Add route `/landlord/dashboard`.
- [ ] Add route `/landlord/rooming-houses/:id`.

---

## 7. Step 4 — Tạo Landlord Dashboard

### File mới

```text
client/src/features/landlord/pages/LandlordDashboardPage.tsx
```

### Nội dung chính

Hiển thị danh sách khu trọ của chủ trọ.

Mỗi card khu trọ gồm:

```text
Ảnh cover
Tên khu trọ
Địa chỉ
Trạng thái approval
Số phòng
Button theo trạng thái
```

### Button theo trạng thái

```text
Approved
  → "Quản lý phòng" → /landlord/rooming-houses/:id

Draft / Rejected
  → "Chỉnh sửa hồ sơ" → /landlord/register?id=xxx

Pending
  → Badge "Đang chờ duyệt", không cho sửa
```

### Nút thêm khu trọ mới

Logic:

```text
Nếu tất cả khu trọ hiện tại Approved
  → enable "Thêm khu trọ mới"

Nếu còn Draft / Pending / Rejected
  → disable
  → hiển thị lý do: cần hoàn tất hồ sơ hiện tại trước
```

### Checklist

- [ ] Load danh sách khu trọ của chủ trọ.
- [ ] Render empty state nếu chưa có khu trọ.
- [ ] Render card khu trọ.
- [ ] Hiển thị badge trạng thái.
- [ ] Button `Quản lý phòng` chỉ hiện/enable khi Approved.
- [ ] Button `Chỉnh sửa hồ sơ` cho Draft/Rejected.
- [ ] Pending read-only.
- [ ] Button thêm khu trọ mới enable đúng điều kiện.

---

## 8. Step 5 — Tạo Rooming House Detail Page

### File mới

```text
client/src/features/landlord/pages/RoomingHouseDetailPage.tsx
```

### Tabs

```text
1. Thông tin khu trọ
2. Ảnh khu trọ
3. Tiện nghi
4. Giấy tờ pháp lý
5. Danh sách phòng
```

### Rule

```text
Chỉ khu trọ Approved mới vào được trang này.
```

### Tab 1 — Thông tin khu trọ

Cho sửa:

```text
Tên
Mô tả
Địa chỉ
Tọa độ nếu có
```

API:

```text
PUT /api/rooming-houses/:id
```

### Tab 2 — Ảnh khu trọ

API:

```text
PUT /api/rooming-houses/:id/images
```

### Tab 3 — Tiện nghi

API:

```text
PUT /api/rooming-houses/:id/amenities
```

### Tab 4 — Giấy tờ

Chỉ xem sau khi Approved.

Không cho sửa giấy tờ pháp lý ở bước này.

### Tab 5 — Danh sách phòng

Hiển thị list phòng và CRUD phòng.

### Checklist

- [ ] Load rooming house detail theo `id`.
- [ ] Nếu không Approved thì redirect dashboard hoặc show error.
- [ ] Tạo tabs.
- [ ] Tab thông tin khu trọ.
- [ ] Tab ảnh khu trọ.
- [ ] Tab tiện nghi.
- [ ] Tab giấy tờ read-only.
- [ ] Tab danh sách phòng.

---

## 9. Step 6 — Tạo Room API Service

### File mới

```text
client/src/features/landlord/services/roomApi.ts
```

### Methods

```ts
export const roomApi = {
  getRoomsByHouse(roomingHouseId: string),
  createRoom(roomingHouseId: string, data),
  getRoom(roomId: string),
  updateRoom(roomId: string, data),
  updateRoomImages(roomId: string, images),
  updateRoomAmenities(roomId: string, amenityIds),
  updateRoomPriceTiers(roomId: string, tiers),
  updateRoomStatus(roomId: string, status)
}
```

### Checklist

- [ ] Tạo `roomApi.ts`.
- [ ] Dùng `apiClient`.
- [ ] Gắn `auth: true`.
- [ ] Dùng endpoints từ `endpoints.ts`.
- [ ] Type response/request đầy đủ.

---

## 10. Step 7 — Update Landlord API

### File

```text
client/src/features/landlord/services/landlordApi.ts
```

### Thêm methods

```ts
getMyRoomingHouses()
checkLandlordEligibility()
```

Cần confirm endpoint chính xác:

```text
GET /api/rooming-houses?ownedByMe=true
hoặc
GET /api/rooming-houses/my
```

và:

```text
GET /api/users/me/landlord-eligibility
```

### Checklist

- [ ] Confirm endpoint danh sách khu trọ của chủ trọ.
- [ ] Add `getMyRoomingHouses`.
- [ ] Add `checkLandlordEligibility`.
- [ ] Update types nếu response khác hiện tại.

---

## 11. Step 8 — Update Endpoints

### File

```text
client/src/shared/api/endpoints.ts
```

### Thêm / chuẩn hóa

```ts
ROOMS: {
  BY_ROOMING_HOUSE: (id: string) => `/api/rooming-houses/${id}/rooms`,
  BY_ID: (id: string) => `/api/rooms/${id}`,
  IMAGES: (id: string) => `/api/rooms/${id}/images`,
  AMENITIES: (id: string) => `/api/rooms/${id}/amenities`,
  PRICE_TIERS: (id: string) => `/api/rooms/${id}/price-tiers`,
  STATUS: (id: string) => `/api/rooms/${id}/status`
}
```

### Checklist

- [ ] Add room endpoints.
- [ ] Confirm existing endpoint names.
- [ ] Không duplicate key đã có.

---

## 12. Step 9 — Update Types

### File

```text
client/src/features/landlord/types/landlord.types.ts
```

### Thêm

```ts
interface RoomResponse
interface CreateRoomRequest
interface UpdateRoomRequest
interface UpdateRoomImagesRequest
interface UpdateRoomAmenitiesRequest
interface UpdateRoomPriceTiersRequest
interface UpdateRoomStatusRequest
interface RoomPriceTier
```

Room status dự kiến:

```text
Available
Occupied
Hidden
Maintenance
```

Cần đối chiếu enum backend trước khi implement.

### Checklist

- [ ] Đọc backend contracts room.
- [ ] Map đúng field casing.
- [ ] Thêm room status union type.
- [ ] Thêm price tier type.
- [ ] Thêm image/amenity types nếu chưa có.

---

## 13. Step 10 — Điều chỉnh LandlordRegisterPage

### File

```text
client/src/features/landlord/pages/LandlordRegisterPage.tsx
```

### Logic mới

```text
Nếu onboarding.canEnterLandlordDashboard === true
  → redirect /landlord/dashboard
```

Ngoại lệ:

```text
Nếu user đi từ dashboard để tạo khu trọ mới
  → vẫn cho vào form 4 tab
```

Có thể dùng query:

```text
/landlord/register?mode=new
/landlord/register?id=roomingHouseId
```

### Checklist

- [ ] Detect `mode=new`.
- [ ] Detect `id`.
- [ ] Nếu landlord vào register không có mode/id thì redirect dashboard.
- [ ] Draft/Rejected vẫn resume/chỉnh sửa được.
- [ ] Pending vẫn read-only hoặc redirect dashboard theo UX.

---

## 14. Step 11 — Room Management UI

Trong `RoomingHouseDetailPage`, tab phòng có:

### List room

```text
Room number
Floor
Area
Max occupants
Status
Min price / max price
```

### Create room form

Fields cơ bản:

```text
Room number
Floor
Area m2
Max occupants
Description
```

### Room detail editor

Các section:

```text
Thông tin cơ bản
Ảnh phòng
Tiện nghi phòng
Bảng giá theo số người
Trạng thái phòng
```

### Checklist

- [ ] Load list rooms.
- [ ] Create room.
- [ ] Edit basic room info.
- [ ] Upload/update room images.
- [ ] Update room amenities.
- [ ] Update price tiers.
- [ ] Update room status.
- [ ] Refresh list sau mỗi mutation.

---

## 15. Step 12 — Guard Cho Landlord Dashboard

Cần tránh tenant thường vào `/landlord/dashboard`.

Logic:

```text
Nếu không có role Landlord
  → redirect /me

Nếu có role Landlord
  → cho vào dashboard
```

Có thể tạo:

```text
LandlordRoute.tsx
```

Hoặc check trực tiếp trong page.

### Checklist

- [ ] Chặn tenant thường.
- [ ] Chặn user chưa login bằng `ProtectedRoute`.
- [ ] Admin không bị redirect nhầm sang landlord dashboard.
- [ ] Landlord vào được dashboard.

---

## 16. Open Questions

### Câu hỏi 1

Endpoint lấy danh sách khu trọ của chủ trọ là gì?

```text
GET /api/rooming-houses?ownedByMe=true
hay
GET /api/rooming-houses/my
```

### Câu hỏi 2

Tạo khu trọ thứ 2 dùng route nào?

```text
/landlord/register?mode=new
hay tạo page riêng
```

### Câu hỏi 3

Khu trọ đã Approved khi sửa thông tin, ảnh, tiện nghi có cần admin duyệt lại không?

Plan hiện tại giả định:

```text
Không cần duyệt lại
```

### Câu hỏi 4

Giấy tờ pháp lý sau Approved có được sửa không?

Plan hiện tại giả định:

```text
Không được sửa, chỉ xem
```

---

## 17. Verification Plan

### Build

```bash
npm run build
```

### Manual test

- [ ] Login tenant.
- [ ] Home hiện `Đăng ký làm chủ trọ`.
- [ ] Chưa KYC bấm đăng ký → về `/me/kyc`.
- [ ] KYC approved bấm đăng ký → vào `/landlord/register`.
- [ ] Submit khu trọ → trạng thái `Pending`.
- [ ] Admin approve khu trọ.
- [ ] User có role Landlord.
- [ ] Home hiện `Kênh chủ trọ`.
- [ ] Vào `/landlord/dashboard`.
- [ ] Dashboard list khu trọ Approved.
- [ ] Click `Quản lý phòng`.
- [ ] Thêm phòng.
- [ ] Sửa phòng.
- [ ] Đổi trạng thái phòng.
- [ ] Build pass, không lỗi TypeScript.

