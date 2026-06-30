# Báo Cáo Unit Test & Coverage — Smart Rental Platform Backend

**Ngày cập nhật:** 2026-06-24  
**Phạm vi báo cáo:** Backend `server`  
**Framework test:** xUnit, EF Core InMemory, ASP.NET Core Integration Test  
**Coverage tool:** coverlet.collector, Cobertura, ReportGenerator  

---

## 1. Tổng Quan Kết Quả Test

| Loại test | Số test case | Kết quả |
|----------|-------------:|---------|
| Unit Test | 157 | 157 Pass, 0 Fail |
| Integration Test | 9 | 9 Pass, 0 Fail |
| **Tổng cộng** | **166** | **166 Pass, 0 Fail** |

Kết quả hiện tại cho thấy toàn bộ test backend đều chạy thành công, không có test case bị fail.

---

## 2. Các Phần Đã Tập Trung Kiểm Thử

### 2.1 Authentication & KYC

Đã test các chức năng chính:

- Đăng ký tài khoản tenant.
- Kiểm tra email đã tồn tại.
- Đăng nhập thành công.
- Đăng nhập khi tài khoản bị banned.
- Khóa tài khoản sau nhiều lần nhập sai mật khẩu.
- Không cho đăng nhập khi email chưa xác thực.
- Submit KYC hợp lệ.
- Validate thiếu file KYC.
- Validate document type không hợp lệ.
- Kiểm tra citizen ID đã được approve cho user khác.
- Lấy trạng thái KYC và lịch sử KYC.

Đánh giá: Các flow chính của auth và KYC đã được kiểm thử tốt.

---

### 2.2 Room, Room Price Tier, Rental Request

Đã test các chức năng:

- Tạo phòng hợp lệ.
- Validate diện tích phòng không hợp lệ.
- Không cho tạo phòng trùng số trong cùng khu trọ.
- Không cho cập nhật phòng đang `Occupied`.
- Cập nhật bảng giá phòng.
- Không cho cập nhật giá khi phòng đang `Occupied`.
- Tạo yêu cầu thuê phòng.
- Validate ngày bắt đầu thuê quá gần.
- Validate tenant đã có request pending cùng phòng.
- Validate landlord không được thuê phòng của chính mình.
- Tenant hủy request.
- Landlord reject request.
- Kiểm tra quyền xem request.
- Lấy danh sách request của tenant và request gửi đến landlord.

Đánh giá: Nhóm rental request đã cover nhiều case nghiệp vụ quan trọng, nhưng flow `ApproveAsync` vẫn còn khó test sâu do có raw SQL/locking behavior.

---

### 2.3 Deposit, Billing, Invoice Payment

Đã test các chức năng:

- Thanh toán tiền cọc.
- Không cho user khác thanh toán deposit không thuộc về mình.
- Không cho thanh toán deposit sai trạng thái.
- Expire deposit quá hạn.
- Tạo contract sau khi deposit được thanh toán.
- Preview invoice.
- Generate invoice với meter readings.
- Issue invoice.
- Cancel invoice.
- Tenant thanh toán invoice bằng ví.
- Validate invoice draft/paid/not found.
- Idempotent payment khi invoice đã paid.

Đánh giá: Các flow billing chính đã được test, nhưng `BillingService` là service lớn nên branch coverage vẫn còn trung bình.

---

### 2.4 Admin Approval

Đã test các chức năng:

- Ghi audit log khi admin xử lý approval.
- Lấy danh sách KYC pending.
- Xem chi tiết KYC.
- Approve KYC.
- Reject KYC.
- Sync thông tin KYC sang user profile.
- Lấy danh sách user cho admin, loại trừ admin user.
- Xem chi tiết user, profile và KYC đã approve.
- Lấy danh sách rooming house pending/public.
- Xem chi tiết rooming house gồm legal document, images, amenities, rooms.
- Approve rooming house.
- Reject rooming house.

Đánh giá: Đây là nhóm được cải thiện mạnh, coverage namespace `AdminApproval` đạt **95.54% line coverage**.

---

### 2.5 Wallet & Payments

Đã test các chức năng:

- Tạo ví cho user active.
- Không tạo ví cho user không tồn tại hoặc bị banned.
- Credit ví.
- Debit ví.
- Tăng/giảm reserved balance.
- Chuyển tiền giữa hai ví.
- Chuyển tiền từ reserved balance.
- Release reserved balance.
- Lấy lịch sử giao dịch ví.
- Xử lý webhook thanh toán thành công.
- Xử lý webhook failed/cancelled.
- Xử lý duplicate webhook.
- Xử lý unmatched webhook.
- Xử lý invalid PayOS signature.
- Validate amount mismatch trong webhook.
- Tạo PayOS top-up.
- Validate top-up amount.
- Validate KYC trước khi top-up.
- Kiểm tra idempotency key.
- Lấy lịch sử top-up và chi tiết top-up.

Đánh giá: Đây là phần được bổ sung nhiều ở lần cập nhật gần nhất. Coverage đạt:

| Namespace | Line coverage | Branch coverage |
|-----------|--------------:|----------------:|
| `SmartRentalPlatform.Application.Wallets` | **85.85%** | **78.75%** |
| `SmartRentalPlatform.Application.Payments` | **81.26%** | **61.83%** |

---

### 2.6 Rooming House Search

Đã test các phần logic tìm kiếm:

- Parse query tiếng Việt có dấu/không dấu.
- Parse giá tiền từ query.
- Parse diện tích.
- Parse số người ở.
- Parse bán kính tìm kiếm.
- Parse địa điểm nổi bật như Đại học FPT Đà Nẵng.
- Validate tọa độ tìm kiếm.
- Normalize bán kính tìm kiếm.
- Tính khoảng cách địa lý.
- Tính bounding box.
- Tính điểm recommendation theo hành vi user, amenities, KYC, hình ảnh, độ mới.

Đánh giá: Nhóm search được bổ sung test cho logic thuần, không cần database phức tạp. Namespace `RoomingHouses.Search` đạt **56.02% line coverage**.

---

## 3. Coverage Nổi Bật Theo Namespace

| Namespace | Line coverage | Branch coverage | Đánh giá |
|-----------|--------------:|----------------:|----------|
| `Application.Administrative` | 100.00% | 100.00% | Xuất sắc |
| `Application.Amenities` | 100.00% | 100.00% | Xuất sắc |
| `Application.Notifications` | 100.00% | 100.00% | Xuất sắc |
| `Application.AdminApproval` | 95.54% | 69.70% | Rất tốt |
| `Application.Kyc` | 89.43% | 58.16% | Tốt |
| `Application.Wallets` | 85.85% | 78.75% | Tốt |
| `Application.RoomDeposits` | 81.53% | 70.00% | Tốt |
| `Application.Payments` | 81.26% | 61.83% | Tốt |
| `Application.Billing` | 62.32% | 41.75% | Trung bình |
| `Application.RoomingHouses.Search` | 56.02% | 54.69% | Khá |
| `Application.RentalRequests` | 53.38% | 42.37% | Trung bình |
| `Application.Rooms` | 30.34% | 31.58% | Cần cải thiện |
| `Application.Auth` | 22.41% | 11.83% | Cần cải thiện namespace/branch |

---

## 4. Line Coverage Là Gì?

**Line coverage** là tỷ lệ phần trăm số dòng code đã được chạy khi test thực thi.

Ví dụ:

```text
Line coverage = 85%
```

Nghĩa là trong phần code được đo, có 85% số dòng đã được test chạy qua ít nhất một lần.

Line coverage cao cho thấy test đã đi qua nhiều phần code, nhưng không đảm bảo mọi nhánh logic đều được kiểm thử đầy đủ.

Ví dụ:

```csharp
if (wallet.Balance < amount)
{
    throw new ConflictException(...);
}

wallet.Balance -= amount;
```

Nếu test chỉ chạy case đủ tiền, dòng trừ tiền được cover. Nhưng nếu chưa test case không đủ tiền, nhánh `throw` chưa được kiểm thử.

---

## 5. Branch Coverage Là Gì?

**Branch coverage** là tỷ lệ phần trăm các nhánh điều kiện đã được test chạy qua.

Các nhánh thường đến từ:

- `if / else`
- `switch`
- toán tử điều kiện
- điều kiện null
- logic validation
- exception path

Ví dụ:

```csharp
if (amount <= 0)
{
    throw new BadRequestException(...);
}

return Success();
```

Để đạt branch coverage tốt, cần test cả hai trường hợp:

| Case | Ý nghĩa |
|------|--------|
| `amount <= 0` | Nhánh lỗi |
| `amount > 0` | Nhánh thành công |

Branch coverage thường khó đạt cao hơn line coverage vì một dòng code có thể chứa nhiều hướng xử lý khác nhau.

---

## 6. Cách Đọc Coverage Report

Khi mở file HTML coverage report, thường sẽ thấy các thông tin sau:

### 6.1 Assemblies / Packages / Namespaces

Đây là các nhóm code được đo coverage.

Trong project này, nên tập trung vào:

- `SmartRentalPlatform.Application`
- `SmartRentalPlatform.Domain`
- `SmartRentalPlatform.Infrastructure`
- `SmartRentalPlatform.Api`

Với môn SWT, phần quan trọng nhất thường là `Application` vì chứa business logic.

---

### 6.2 Classes

Mỗi namespace có nhiều class. Ví dụ:

- `WalletService`
- `PaymentWebhookService`
- `BillingService`
- `RentalRequestService`
- `KycService`

Khi click vào từng class trong HTML report, có thể xem:

- Dòng nào đã được test chạy qua.
- Dòng nào chưa được test.
- Nhánh nào đã/chưa được cover.

---

### 6.3 Covered Lines

Là số dòng đã được test chạy qua.

Ví dụ:

```text
Covered Lines: 358
Total Lines: 417
Line Coverage: 85.85%
```

Nghĩa là trong `WalletService`, có 358/417 dòng đã được test chạy qua.

---

### 6.4 Uncovered Lines

Là các dòng chưa được test chạy qua.

Các dòng uncovered thường là:

- Nhánh exception chưa test.
- Flow success phức tạp chưa test.
- Code phụ thuộc API ngoài.
- Code phụ thuộc raw SQL/transaction/locking.
- Code generate file/PDF/storage.

---

### 6.5 Branches

Branches là các hướng rẽ trong logic.

Ví dụ:

```csharp
if (user is null)
{
    throw new NotFoundException(...);
}

if (user.Status != UserStatus.Active)
{
    throw new ForbiddenException(...);
}
```

Đoạn này có nhiều branch:

- User tồn tại.
- User không tồn tại.
- User active.
- User không active.

Muốn branch coverage cao thì phải có test cho từng hướng xử lý.

---

## 7. Vì Sao Raw Coverage Toàn Project Còn Thấp?

Raw Cobertura toàn unit coverage hiện là:

| Metric | Giá trị |
|--------|--------:|
| Line coverage | 4.44% |
| Branch coverage | 21.76% |

Con số raw này thấp vì coverage tool đang tính rất rộng, bao gồm cả:

- Migration/generated code.
- Infrastructure configuration.
- Entity configuration.
- Seeder.
- Worker/background service.
- Các module lớn chưa nằm trong phạm vi test hiện tại.

Vì vậy khi đánh giá môn SWT, nên đọc song song:

- Raw coverage toàn solution.
- Coverage của các namespace nghiệp vụ trong `Application`.
- Danh sách test case đã viết.
- Các flow nghiệp vụ đã được kiểm thử.

---

## 8. Những Phần Chưa Cover Tốt

Các phần còn thấp hoặc chưa có test trực tiếp:

| Module | Lý do |
|--------|------|
| `RentalContracts` | Module lớn, nhiều flow ký hợp đồng, PDF, file, occupant |
| `RoomingHouses` | Nhiều service liên quan media, legal document, onboarding |
| `Users` | Chưa có test trực tiếp cho user profile/session |
| `ViewingAppointments` | Chưa có test trực tiếp |
| `BillingService` | Service lớn, nhiều branch termination/final invoice/meter reading |
| `RentalRequestService.ApproveAsync` | Khó unit test do raw SQL/locking |
| `Auth` branch | Cần thêm OTP/refresh/logout/password/session flows |

---

## 9. Kết Luận

Bộ test backend hiện có **166 test cases**, tất cả đều **PASS**.

Các phần đã test tốt nhất:

- Admin approval.
- Wallet.
- Payments.
- Notifications.
- Amenities.
- Administrative catalog.
- Room deposits.
- KYC.
- Invoice wallet payment.

Các phần mới được cải thiện mạnh:

- `WalletService`
- `PaymentWebhookService`
- `PayOSTopUpService`
- `RoomingHouseSearchParser`
- `GeoSearchHelper`
- `RuleBasedRoomingHouseRecommendationScorer`

Nhìn chung, test suite hiện đã kiểm thử được nhiều flow nghiệp vụ quan trọng của backend. Những phần còn lại cần test thêm chủ yếu là các module lớn hoặc phụ thuộc nhiều vào hạ tầng như contract, rooming house onboarding, PDF/file/storage và raw SQL transaction.
