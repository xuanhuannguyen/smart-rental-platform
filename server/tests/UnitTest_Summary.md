# Unit Test Summary — Smart Rental Platform Backend

**Ngày cập nhật:** 2026-06-24  
**Framework:** xUnit + EF Core InMemory + ASP.NET Core Integration Test  
**Coverage tool:** coverlet.collector + Cobertura  
**Tổng kết:** 166 test cases | 166 Pass | 0 Fail

---

## 1. Tổng Quan Test Suite

| Nhóm test | Số test class | Số test case | Pass | Fail | Đánh giá |
|-----------|--------------:|-------------:|-----:|-----:|----------|
| Unit Tests | 19 | 157 | 157 | 0 | Tốt |
| Integration Tests | 5 | 9 | 9 | 0 | Tốt |
| **Tổng** | **24** | **166** | **166** | **0** | Không có regression |

---

## 2. Danh Sách Test Class

| # | Test Class | Loại | Class / API Under Test | Số TC | Pass | Fail |
|---|------------|------|------------------------|------:|-----:|-----:|
| 1 | `AuthServiceTests` | Unit | `AuthService` | 6 | 6 | 0 |
| 2 | `KycServiceTests` | Unit | `KycService` | 7 | 7 | 0 |
| 3 | `RoomCommandServiceTests` | Unit | `RoomCommandService` | 5 | 5 | 0 |
| 4 | `RoomPriceTierServiceTests` | Unit | `RoomPriceTierService` | 2 | 2 | 0 |
| 5 | `RentalRequestServiceTests` | Unit | `RentalRequestService` | 18 | 18 | 0 |
| 6 | `RoomDepositServiceTests` | Unit | `RoomDepositService` | 7 | 7 | 0 |
| 7 | `BillingServiceTests` | Unit | `BillingService` | 30 | 30 | 0 |
| 8 | `InvoiceWalletPaymentServiceTests` | Unit | `InvoiceWalletPaymentService` | 5 | 5 | 0 |
| 9 | `AdministrativeServiceTests` | Unit | `AdministrativeService` | 3 | 3 | 0 |
| 10 | `AmenityServiceTests` | Unit | `AmenityService` | 4 | 4 | 0 |
| 11 | `NotificationServiceTests` | Unit | `NotificationService` | 8 | 8 | 0 |
| 12 | `ApprovalAuditServiceTests` | Unit | `ApprovalAuditService` | 1 | 1 | 0 |
| 13 | `AdminKycApprovalServiceTests` | Unit | `AdminKycApprovalService` | 8 | 8 | 0 |
| 14 | `AdminUserServiceTests` | Unit | `AdminUserService` | 3 | 3 | 0 |
| 15 | `AdminRoomingHouseApprovalServiceTests` | Unit | `AdminRoomingHouseApprovalService` | 8 | 8 | 0 |
| 16 | `RoomingHouseSearchUtilityTests` | Unit | Search parser / geo / scorer | 11 | 11 | 0 |
| 17 | `WalletServiceTests` | Unit | `WalletService` | 13 | 13 | 0 |
| 18 | `PaymentWebhookServiceTests` | Unit | `PaymentWebhookService`, `MockPaymentService` | 9 | 9 | 0 |
| 19 | `PayOSTopUpServiceTests` | Unit | `PayOSTopUpService` | 9 | 9 | 0 |
| 20 | `AuthControllerTests` | Integration | `AuthController` | 3 | 3 | 0 |
| 21 | `RoomControllerTests` | Integration | `RoomsController` | 3 | 3 | 0 |
| 22 | `RentalRequestControllerTests` | Integration | `RentalRequestsController` | 1 | 1 | 0 |
| 23 | `RoomDepositControllerTests` | Integration | `RoomDepositsController` | 1 | 1 | 0 |
| 24 | `BillingControllerTests` | Integration | `BillingController` | 1 | 1 | 0 |
| | **TỔNG** | | | **166** | **166** | **0** |

---

## 3. Chi Tiết Test Cases Theo Class

### 3.1 AuthServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `RegisterAsync` — tạo tài khoản Tenant hợp lệ, gắn role Tenant và tạo OTP xác thực email | PASS |
| TC02 | `RegisterAsync` — email đã tồn tại → `ConflictException` | PASS |
| TC03 | `LoginAsync` — credential hợp lệ → trả access token và refresh token | PASS |
| TC04 | `LoginAsync` — tài khoản bị banned → `ForbiddenException` | PASS |
| TC05 | `LoginAsync` — nhập sai mật khẩu 5 lần → khóa tài khoản | PASS |
| TC06 | `LoginAsync` — email chưa xác thực → `ForbiddenException` | PASS |

---

### 3.2 KycServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `SubmitAsync` — thiếu file bắt buộc → `KycBusinessException` | PASS |
| TC02 | `SubmitAsync` — input và mock eKYC hợp lệ → tạo hồ sơ `PendingAdminReview` | PASS |
| TC03 | `GetMyStatusAsync` — user chưa có KYC → `HasSubmission = false` | PASS |
| TC04 | `SubmitAsync` — document type không hợp lệ → `KycBusinessException` | PASS |
| TC05 | `SubmitAsync` — citizen ID đã approve cho user khác → `KycBusinessException` | PASS |
| TC06 | `GetMyStatusAsync` — user đã có KYC → trả hồ sơ mới nhất | PASS |
| TC07 | `GetMyHistoryAsync` — trả lịch sử KYC theo `SubmittedAt` giảm dần | PASS |

---

### 3.3 RoomCommandServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `CreateAsync` — thiếu service price bắt buộc của khu trọ → `BadRequestException` | PASS |
| TC02 | `CreateAsync` — dữ liệu hợp lệ → tạo room, mặc định trạng thái `Hidden` | PASS |
| TC03 | `CreateAsync` — diện tích phòng <= 0 → `BadRequestException` | PASS |
| TC04 | `CreateAsync` — số phòng trùng trong cùng khu trọ → `ConflictException` | PASS |
| TC05 | `UpdateAsync` — room đang `Occupied` → `ConflictException` | PASS |

---

### 3.4 RoomPriceTierServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `UpdatePriceTiersAsync` — room đang `Occupied` → `ConflictException` | PASS |
| TC02 | `UpdatePriceTiersAsync` — room có thể chỉnh sửa → thay thế bảng giá cũ bằng bảng giá mới | PASS |

---

### 3.5 RentalRequestServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `CreateAsync` — ngày bắt đầu thuê quá gần → `BadRequestException` | PASS |
| TC02 | `CreateAsync` — room available và policy hợp lệ → tạo request `Pending` | PASS |
| TC03 | `CreateAsync` — tenant đã có pending request cùng phòng → `ConflictException` | PASS |
| TC04 | `CancelAsync` — tenant sở hữu request và request `Pending` → chuyển `Cancelled` | PASS |
| TC05 | `CancelAsync` — tenant không sở hữu request → `ForbiddenException` | PASS |
| TC06 | `CreateAsync` — room không tồn tại → `NotFoundException` | PASS |
| TC07 | `CreateAsync` — landlord tự thuê phòng của chính mình → `ForbiddenException` | PASS |
| TC08 | `CreateAsync` — khu trọ chưa cấu hình rental policy → `ConflictException` | PASS |
| TC09 | `CreateAsync` — số người vượt quá sức chứa phòng → `BadRequestException` | PASS |
| TC10 | `RejectAsync` — landlord hợp lệ từ chối request pending → chuyển `Rejected` | PASS |
| TC11 | `RejectAsync` — lý do từ chối rỗng → `BadRequestException` | PASS |
| TC12 | `GetMyRequestsAsync` — chỉ trả request của tenant hiện tại | PASS |
| TC13 | `GetByIdAsync` — user không được xem request → `ForbiddenException` | PASS |
| TC14 | `GetIncomingRequestsAsync` — chỉ trả request thuộc landlord hiện tại | PASS |
| TC15 | `GetByIdAsync` — request không tồn tại → `null` | PASS |
| TC16 | `ApproveAsync` — thiếu payment deadline → `BadRequestException` | PASS |
| TC17 | `RejectAsync` — landlord không sở hữu request → `ForbiddenException` | PASS |
| TC18 | `CancelAsync` — request không còn `Pending` → `ConflictException` | PASS |

---

### 3.6 RoomDepositServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `PayAsync` — user thanh toán không phải tenant của deposit → `ForbiddenException` | PASS |
| TC02 | `PayAsync` — deposit không ở trạng thái `PendingPayment` → `ConflictException` | PASS |
| TC03 | `PayAsync` — deposit không tồn tại → `null` | PASS |
| TC04 | `ExpireOverdueAsync` — deposit/request hết hạn và room `Reserved` được trả về `Available` | PASS |
| TC05 | `PayAsync` — pending deposit thanh toán thành công → deposit `Paid` và tạo contract | PASS |
| TC06 | `PayAsync` — deposit đã quá hạn → `ConflictException` | PASS |
| TC07 | `PayAsync` — paid deposit thiếu contract → `ConflictException` | PASS |

---

### 3.7 BillingServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `GetRoomBillingContextAsync` — room không có active contract → `NotFoundException` | PASS |
| TC02 | `GetRoomBillingContextAsync` — room có active contract → trả billing context | PASS |
| TC03 | `GetRoomInvoicePreviewAsync` — period end trước start → `BadRequestException` | PASS |
| TC04 | `GetRoomInvoicePreviewAsync` — kỳ hóa đơn vượt tháng → `BadRequestException` | PASS |
| TC05 | `GetRoomInvoicePreviewAsync` — không có active contract → `NotFoundException` | PASS |
| TC06 | `GetRoomInvoicePreviewAsync` — thiếu bảng giá dịch vụ → trả block reason | PASS |
| TC07 | `GetRoomInvoicePreviewAsync` — đủ bảng giá dịch vụ → preview có thể generate | PASS |
| TC08 | `IssueInvoiceAsync` — invoice `Draft` → chuyển `Issued` | PASS |
| TC09 | `IssueInvoiceAsync` — invoice không phải `Draft` → `BadRequestException` | PASS |
| TC10 | `GetBillingServiceTypesAsync` — chỉ trả service type active và sắp xếp theo tên | PASS |
| TC11 | `GetLandlordInvoicesAsync` — filter theo landlord/status/search/contract | PASS |
| TC12 | `GetLandlordInvoiceAsync` — landlord không sở hữu invoice → `ForbiddenException` | PASS |
| TC13 | `CancelInvoiceAsync` — invoice issued → chuyển `Cancelled` và trim reason | PASS |
| TC14 | `CancelInvoiceAsync` — invoice đã `Paid` → `BadRequestException` | PASS |
| TC15 | `GetMyInvoicesAsync` — không trả invoice `Draft` | PASS |
| TC16 | `GetMyInvoiceAsync` — invoice là `Draft` → `NotFoundException` | PASS |
| TC17 | `PayInvoiceAsync` — wallet payment thành công → trả invoice cập nhật | PASS |
| TC18 | `PayInvoiceAsync` — wallet payment thất bại → `BadRequestException` | PASS |
| TC19 | `GenerateInvoiceWithReadingsAsync` — period end trước start → `BadRequestException` | PASS |
| TC20 | `GenerateInvoiceWithReadingsAsync` — kỳ hóa đơn vượt tháng → `BadRequestException` | PASS |
| TC21 | `GenerateInvoiceWithReadingsAsync` — discount âm → `BadRequestException` | PASS |
| TC22 | `GenerateInvoiceWithReadingsAsync` — meter readings null → `BadRequestException` | PASS |
| TC23 | `GenerateInvoiceWithReadingsAsync` — input hợp lệ → tạo invoice `Draft`, invoice items và meter readings | PASS |
| TC24 | `GetLandlordInvoiceAsync` — invoice không tồn tại → `NotFoundException` | PASS |
| TC25 | `GetTerminationInvoicePreviewAsync` — contract snapshot không tồn tại → `NotFoundException` | PASS |
| TC26 | `GetTerminationInvoicePreviewAsync` — contract thiếu termination date → `ConflictException` | PASS |
| TC27 | `GetTerminationInvoicePreviewAsync` — landlord không sở hữu contract → `ForbiddenException` | PASS |
| TC28 | `CreateNextTerminationInvoiceAsync` — contract chưa có termination date → `ConflictException` | PASS |
| TC29 | `CreateFinalInvoiceForTerminationAsync` — discount âm → `BadRequestException` | PASS |
| TC30 | `CreateFinalInvoiceForTerminationAsync` — meter readings null → `BadRequestException` | PASS |

---

### 3.8 InvoiceWalletPaymentServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `PayInvoiceAsync` — user thanh toán không phải tenant của invoice → `ForbiddenException` | PASS |
| TC02 | `PayInvoiceAsync` — invoice còn `Draft` → `BadRequestException` | PASS |
| TC03 | `PayInvoiceAsync` — invoice không tồn tại → `NotFoundException` | PASS |
| TC04 | `PayInvoiceAsync` — invoice `Issued` → chuyển `Paid` và lưu transfer group | PASS |
| TC05 | `PayInvoiceAsync` — invoice đã `Paid` → idempotent success | PASS |

---

### 3.9 AdministrativeServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `GetActiveProvincesAsync` — chỉ trả province active và sắp xếp theo tên | PASS |
| TC02 | `GetWardsByProvinceAsync` — province code rỗng → danh sách rỗng | PASS |
| TC03 | `GetWardsByProvinceAsync` — chỉ trả ward active thuộc province và sắp xếp theo tên | PASS |

---

### 3.10 AmenityServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `GetActiveAmenitiesAsync` — không filter scope → trả tất cả amenity active | PASS |
| TC02 | `GetActiveAmenitiesAsync` — scope House → gồm House và Both | PASS |
| TC03 | `GetActiveAmenitiesAsync` — scope Room → gồm Room và Both | PASS |
| TC04 | `GetActiveAmenitiesAsync` — scope Both → chỉ trả Both | PASS |

---

### 3.11 NotificationServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `CreateAsync` — lưu notification unread kèm reference | PASS |
| TC02 | `GetUnreadCountAsync` — chỉ đếm unread của user hiện tại | PASS |
| TC03 | `GetNotificationsAsync` — trả notification theo thời gian giảm dần và limit | PASS |
| TC04 | `MarkAsReadAsync` — notification thuộc user → chuyển read | PASS |
| TC05 | `MarkAsReadAsync` — notification không tồn tại → `NotFoundException` | PASS |
| TC06 | `MarkAllAsReadAsync` — chỉ mark unread của user hiện tại | PASS |
| TC07 | `DeleteAsync` — notification thuộc user → xóa | PASS |
| TC08 | `DeleteAsync` — notification không tồn tại → `NotFoundException` | PASS |

---

### 3.12 AdminApproval Service Tests *(Unit)*

| Test Class | Số TC | Phạm vi kiểm thử | Kết quả |
|------------|------:|------------------|---------|
| `ApprovalAuditServiceTests` | 1 | Ghi audit log admin approval | PASS |
| `AdminKycApprovalServiceTests` | 8 | Pending/detail/history, approve, reject, sync profile | PASS |
| `AdminUserServiceTests` | 3 | List user loại admin, detail/profile/latest approved KYC | PASS |
| `AdminRoomingHouseApprovalServiceTests` | 8 | Pending/public list, detail mapping, approve/reject | PASS |

---

### 3.13 RoomingHouseSearchUtilityTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `Parse` — query có giá, diện tích, số người, bán kính, địa điểm, amenities → parse đúng criteria | PASS |
| TC02 | `Parse` — request đã có filter explicit → không bị query override | PASS |
| TC03 | `Normalize` — `Sai Gon` → `sai gon` | PASS |
| TC04 | `Normalize` — `Đà Nẵng` → `da nang` | PASS |
| TC05 | `Normalize` — gom khoảng trắng và bỏ dấu | PASS |
| TC06 | `GeoSearchHelper` — tính khoảng cách và bounding box | PASS |
| TC07 | `ValidateCoordinates` — lat/lng sai hoặc thiếu cặp tọa độ → `BadRequestException` | PASS |
| TC08 | `NormalizeRadius` — default radius, valid radius và invalid range | PASS |
| TC09 | `RuleBasedScorer` — áp dụng recent penalty, amenity bonus, image bonus, KYC bonus, freshness bonus | PASS |
| TC10 | `RuleBasedScorer` — nhà cũ hơn nhận freshness bonus thấp hơn | PASS |
| TC11 | `NoopIntentEnricher` — hoàn tất không thay đổi criteria AI | PASS |

---

### 3.14 WalletServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `GetMyWalletAsync` — user active chưa có ví → tạo ví mới | PASS |
| TC02 | `GetOrCreateWalletAsync` — user không tồn tại → `NotFoundException` | PASS |
| TC03 | `GetOrCreateWalletAsync` — user inactive/banned → `ForbiddenException` | PASS |
| TC04 | `CreditAsync` và `DebitAsync` — cập nhật balance và tạo transaction | PASS |
| TC05 | `DebitAsync` — amount không hợp lệ → `BadRequestException` | PASS |
| TC06 | `DebitAsync` — không đủ balance → `ConflictException` | PASS |
| TC07 | `IncreaseReservedAsync` và `DecreaseReservedAsync` — cập nhật reserved balance | PASS |
| TC08 | `TransferAsync` — chuyển tiền giữa hai ví và dùng chung transfer group | PASS |
| TC09 | `TransferAsync` — source và target cùng ví → `BadRequestException` | PASS |
| TC10 | `TransferFromReservedWithinTransactionAsync` — release reserved và transfer amount | PASS |
| TC11 | `TransferFromReservedWithinTransactionAsync` — reserved amount invalid → `BadRequestException` | PASS |
| TC12 | `ReleaseReservedWithinTransactionAsync` — đủ reserved → giảm reserved balance | PASS |
| TC13 | `GetTransactionsAsync` — phân trang và sắp xếp transaction mới nhất | PASS |

---

### 3.15 PaymentWebhookServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `ProcessMockWebhookAsync` — success webhook → credit wallet và payment `Succeeded` | PASS |
| TC02 | `ProcessMockWebhookAsync` — payload lặp lại → `Duplicate` | PASS |
| TC03 | `ProcessMockWebhookAsync` — failed webhook → payment `Failed`, không credit wallet | PASS |
| TC04 | `ProcessMockWebhookAsync` — cancelled webhook → payment `Cancelled`, không credit wallet | PASS |
| TC05 | `ProcessMockWebhookAsync` — không tìm thấy payment transaction → `Unmatched` | PASS |
| TC06 | `ProcessPayOSWebhookAsync` — invalid signature → log `Failed`, payment vẫn `Pending` | PASS |
| TC07 | `ProcessMockWebhookAsync` — amount mismatch → log `Failed`, payment vẫn `Pending` | PASS |
| TC08 | `ProcessMockWebhookAsync` — payment đã `Succeeded` → duplicate theo transaction | PASS |
| TC09 | `MockPaymentService.SimulateFailedAsync` — build mock payload và xử lý webhook failed | PASS |

---

### 3.16 PayOSTopUpServiceTests *(Unit)*

| TC | Mô tả | Kết quả |
|----|-------|---------|
| TC01 | `CreateTopUpAsync` — amount dưới minimum → `BadRequestException` | PASS |
| TC02 | `CreateTopUpAsync` — user không tồn tại → `NotFoundException` | PASS |
| TC03 | `CreateTopUpAsync` — user chưa approved KYC → `ForbiddenException` | PASS |
| TC04 | `CreateTopUpAsync` — input hợp lệ → tạo pending payment và gọi PayOS client | PASS |
| TC05 | `CreateTopUpAsync` — idempotency key đã tồn tại cùng amount → trả payment cũ | PASS |
| TC06 | `CreateTopUpAsync` — idempotency key đã tồn tại khác amount → `ConflictException` | PASS |
| TC07 | `GetTopUpHistoryAsync` — chỉ trả top-up của user hiện tại, phân trang mới nhất | PASS |
| TC08 | `GetTopUpAsync` — top-up thuộc user → trả chi tiết | PASS |
| TC09 | `GetTopUpAsync` — top-up không tồn tại → `NotFoundException` | PASS |

---

### 3.17 AuthControllerTests *(Integration)*

| TC | Endpoint | Mô tả | Kết quả |
|----|----------|-------|---------|
| TC01 | `POST /api/auth/register` | Email đã tồn tại → `409 Conflict` | PASS |
| TC02 | `POST /api/auth/login` | User không tồn tại → `401 Unauthorized` | PASS |
| TC03 | `POST /api/auth/login` | Email chưa xác thực → `403 Forbidden` | PASS |

---

### 3.18 RoomControllerTests *(Integration)*

| TC | Endpoint | Mô tả | Kết quả |
|----|----------|-------|---------|
| TC01 | `GET /api/rooming-houses/{id}/rooms` | Landlord hợp lệ truy cập danh sách room → `200 OK` | PASS |
| TC02 | `POST /api/rooming-houses/{id}/rooms` | Diện tích phòng không hợp lệ → `400 BadRequest` | PASS |
| TC03 | `GET /api/rooms/{id}` | Room không tồn tại → `404 NotFound` | PASS |

---

### 3.19 RentalRequestControllerTests *(Integration)*

| TC | Endpoint | Mô tả | Kết quả |
|----|----------|-------|---------|
| TC01 | `POST /api/rooms/{id}/rental-requests` | Ngày bắt đầu thuê quá gần → `400 BadRequest` | PASS |

---

### 3.20 RoomDepositControllerTests *(Integration)*

| TC | Endpoint | Mô tả | Kết quả |
|----|----------|-------|---------|
| TC01 | `POST /api/room-deposits/{id}/pay` | Deposit không tồn tại → `404 NotFound` | PASS |

---

### 3.21 BillingControllerTests *(Integration)*

| TC | Endpoint | Mô tả | Kết quả |
|----|----------|-------|---------|
| TC01 | `GET /api/billing/service-types` | Lấy danh sách loại dịch vụ billing → `200 OK` | PASS |

---

## 4. Kết Quả Coverage

| Scope | Line coverage | Branch coverage | Đánh giá |
|-------|--------------:|----------------:|----------|
| Raw Cobertura toàn unit coverage XML | 4.44% | 21.76% | Bị kéo thấp do tính cả generated/infrastructure/migration code |
| Selected application services đang test | Cải thiện rõ rệt | Cải thiện rõ rệt | Các service mới thêm test đạt coverage tốt |

---

## 5. Coverage Theo Class Chính

| Class | Line coverage | Branch coverage | Đánh giá |
|-------|--------------:|----------------:|----------|
| `InvoiceWalletPaymentService` | 100.00% | 100.00% | Xuất sắc |
| `RoomPriceTierService` | 100.00% | 100.00% | Xuất sắc |
| `AdministrativeService` | 100.00% | 100.00% | Xuất sắc |
| `AmenityService` | 100.00% | 100.00% | Xuất sắc |
| `NotificationService` | 100.00% | 100.00% | Xuất sắc |
| `ApprovalAuditService` | 100.00% | 100.00% | Xuất sắc |
| `RuleBasedRoomingHouseRecommendationScorer` | 100.00% | 100.00% | Xuất sắc |
| `GeoSearchHelper` | 93.33% | 96.15% | Rất tốt |
| `RoomCommandService` | 94.11% | 50.00% | Tốt |
| `RoomDepositService` | 92.94% | 75.00% | Tốt |
| `PaymentWebhookService` | 88.73% | 72.92% | Rất tốt |
| `MockPaymentService` | 88.57% | 50.00% | Tốt |
| `WalletService` | 85.71% | 73.75% | Tốt |
| `KycService` | 84.14% | 53.57% | Tốt |
| `RoomingHouseSearchParser` | 77.53% | 50.68% | Khá |
| `PayOSTopUpService` | 75.43% | 62.50% | Khá |
| `BillingService` | 63.36% | 27.53% | Trung bình |
| `RentalRequestService` | 50.58% | 45.16% | Trung bình |
| `AuthService` | 100.00% | 31.25% | Cần tăng branch OTP/session |

---

## 6. Coverage Theo Namespace

| Namespace | Covered Lines | Total Lines | Line coverage | Branch coverage | Đánh giá |
|-----------|--------------:|------------:|--------------:|----------------:|----------|
| `SmartRentalPlatform.Application.Administrative` | 32 | 32 | **100.00%** | 100.00% | Xuất sắc |
| `SmartRentalPlatform.Application.Amenities` | 28 | 28 | **100.00%** | 100.00% | Xuất sắc |
| `SmartRentalPlatform.Application.Notifications` | 71 | 71 | **100.00%** | 100.00% | Xuất sắc |
| `SmartRentalPlatform.Application.AdminApproval` | 493 | 516 | **95.54%** | 69.70% | Rất tốt |
| `SmartRentalPlatform.Application.Kyc` | 237 | 265 | **89.43%** | 58.16% | Tốt |
| `SmartRentalPlatform.Application.Wallets` | 358 | 417 | **85.85%** | 78.75% | Tốt |
| `SmartRentalPlatform.Application.RoomDeposits` | 203 | 249 | **81.53%** | 70.00% | Tốt |
| `SmartRentalPlatform.Application.Payments` | 529 | 651 | **81.26%** | 61.83% | Tốt |
| `SmartRentalPlatform.Application.Billing` | 1,128 | 1,810 | **62.32%** | 41.75% | Trung bình |
| `SmartRentalPlatform.Application.RoomingHouses.Search` | 526 | 939 | **56.02%** | 54.69% | Khá |
| `SmartRentalPlatform.Application.RentalRequests` | 308 | 577 | **53.38%** | 42.37% | Trung bình |
| `SmartRentalPlatform.Application.Rooms` | 216 | 712 | **30.34%** | 31.58% | Cần cải thiện |
| `SmartRentalPlatform.Application.Auth` | 177 | 790 | **22.41%** | 11.83% | Cần cải thiện |
| `SmartRentalPlatform.Application.Common.Exceptions` | 48 | 66 | **72.73%** | n/a | Tốt |
| `SmartRentalPlatform.Application.Common.Interfaces` | 42 | 90 | **46.67%** | n/a | Trung bình |
| `SmartRentalPlatform.Application.RentalContracts` | 0 | 9,150 | **0.00%** | 0.00% | Chưa có test trực tiếp |
| `SmartRentalPlatform.Application.RoomingHouses` | 0 | 6,442 | **0.00%** | 0.00% | Chưa có test trực tiếp |
| `SmartRentalPlatform.Application.Users` | 0 | 884 | **0.00%** | 0.00% | Chưa có test trực tiếp |
| `SmartRentalPlatform.Application.ViewingAppointments` | 0 | 994 | **0.00%** | 0.00% | Chưa có test trực tiếp |

---

## 7. Phần Đã Bổ Sung Ở Lần Cập Nhật Này

| Nhóm bổ sung | Test class | Số TC | Tác động coverage |
|--------------|------------|------:|-------------------|
| Search | `RoomingHouseSearchUtilityTests` | 11 | Tăng `RoomingHouses.Search` lên 56.02% line |
| Wallet | `WalletServiceTests` | 13 | Tăng `Wallets` lên 85.85% line |
| Payments | `PaymentWebhookServiceTests` | 9 | Cover webhook success/failed/cancelled/duplicate/unmatched/signature |
| Payments | `PayOSTopUpServiceTests` | 9 | Cover top-up validation, KYC, idempotency, history/detail |

---

## 8. Đánh Giá Chung

### Điểm Mạnh

- 166/166 test cases đều PASS.
- Unit test và integration test đều chạy thành công.
- Các nhóm `AdminApproval`, `Wallets`, `Payments`, `Administrative`, `Amenities`, `Notifications`, `RoomDeposits`, `Kyc` hiện có coverage tốt.
- Các flow nghiệp vụ quan trọng đã được kiểm thử: auth, KYC, room, rental request, deposit, billing, invoice payment, admin approval, wallet mutation, wallet transfer, payment webhook, PayOS top-up, search parser/scoring.
- Coverage của các namespace trước đó gần như chưa có test đã tăng đáng kể, đặc biệt `Payments` và `Wallets`.

### Điểm Cần Cải Thiện

- `RentalContracts` và `RoomingHouses` vẫn là hai module lớn chưa có test trực tiếp, nên raw Cobertura toàn solution còn thấp.
- `BillingService` còn nhiều branch phức tạp cần test thêm nếu muốn tăng branch coverage.
- `RentalRequestService.ApproveAsync` vẫn khó cover success path do phụ thuộc raw SQL/locking behavior.
- `Auth` cần bổ sung thêm OTP/refresh/logout/session/password flows để tăng namespace và branch coverage.

### Kết Luận

Bộ test backend hiện đạt trạng thái ổn định với **166 test cases PASS**. Coverage đã được cải thiện mạnh ở các module nghiệp vụ có thể unit test tốt như `Payments`, `Wallets`, `AdminApproval`, `Notifications`, `Amenities`, `Administrative` và `RoomingHouses.Search`. Các phần còn thấp chủ yếu là module lớn, nhiều phụ thuộc hạ tầng hoặc cần integration test chuyên sâu hơn.
