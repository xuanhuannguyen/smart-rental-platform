# Smart Rental Platform - Unit & Integration Test Scenarios

Tài liệu này phản ánh kịch bản kiểm thử đang khớp với base code hiện tại, không bám máy móc theo plan ban đầu nếu contract/controller/service chưa hỗ trợ behavior đó.

## Current Test Count

- Unit tests: 80
- Integration tests: 9
- Total: 89

Breakdown theo file unit test:

- `AuthServiceTests.cs`: 6
- `KycServiceTests.cs`: 7
- `RoomCommandServiceTests.cs`: 5
- `RoomPriceTierServiceTests.cs`: 2
- `RentalRequestServiceTests.cs`: 18
- `RoomDepositServiceTests.cs`: 7
- `BillingServiceTests.cs`: 30
- `InvoiceWalletPaymentServiceTests.cs`: 5

## Unit Tests

### Auth / Account / KYC

- `AuthService.RegisterAsync` tạo tài khoản Tenant hợp lệ, gắn role Tenant và tạo OTP xác thực email.
- `AuthService.RegisterAsync` trả `ConflictException` khi email đã tồn tại.
- `AuthService.LoginAsync` trả access token và refresh token khi credential hợp lệ.
- `AuthService.LoginAsync` trả `ForbiddenException` khi tài khoản bị banned.
- `AuthService.LoginAsync` khóa tài khoản khi nhập sai mật khẩu 5 lần.
- `AuthService.LoginAsync` trả `ForbiddenException` khi email chưa xác thực.
- `KycService.SubmitAsync` trả `KycBusinessException` khi thiếu file bắt buộc.
- `KycService.SubmitAsync` tạo hồ sơ KYC `PendingAdminReview` khi input và mock eKYC hợp lệ.
- `KycService.SubmitAsync` trả `KycBusinessException` khi document type không hợp lệ.
- `KycService.SubmitAsync` trả `KycBusinessException` khi citizen ID đã được approve cho user khác.
- `KycService.GetMyStatusAsync` trả `HasSubmission = false` khi user chưa có hồ sơ KYC.
- `KycService.GetMyStatusAsync` trả hồ sơ mới nhất khi user đã có KYC.
- `KycService.GetMyHistoryAsync` trả lịch sử KYC theo `SubmittedAt` giảm dần.

### Property / Room

- `RoomCommandService.CreateAsync` trả `BadRequestException` khi khu trọ thiếu service price bắt buộc.
- `RoomCommandService.CreateAsync` tạo room hợp lệ và mặc định room ở trạng thái `Hidden`.
- `RoomCommandService.CreateAsync` trả `BadRequestException` khi diện tích phòng nhỏ hơn hoặc bằng 0.
- `RoomCommandService.CreateAsync` trả `ConflictException` khi số phòng bị trùng trong cùng khu trọ.
- `RoomCommandService.UpdateAsync` trả `ConflictException` khi room đang `Occupied`.
- `RoomPriceTierService.UpdatePriceTiersAsync` trả `ConflictException` khi room đang `Occupied`.
- `RoomPriceTierService.UpdatePriceTiersAsync` thay thế bảng giá cũ bằng bảng giá mới khi room có thể chỉnh sửa.

### Contract / Deposit

- `RentalRequestService.CreateAsync` trả `BadRequestException` khi ngày bắt đầu mong muốn quá gần.
- `RentalRequestService.CreateAsync` tạo rental request `Pending` khi room available và policy hợp lệ.
- `RentalRequestService.CreateAsync` trả `ConflictException` khi tenant đã có pending request cho cùng phòng.
- `RentalRequestService.CreateAsync` trả `NotFoundException` khi room không tồn tại.
- `RentalRequestService.CreateAsync` trả `ForbiddenException` khi landlord tự thuê phòng của chính mình.
- `RentalRequestService.CreateAsync` trả `ConflictException` khi khu trọ chưa cấu hình rental policy.
- `RentalRequestService.CreateAsync` trả `BadRequestException` khi số người vượt quá sức chứa phòng.
- `RentalRequestService.GetMyRequestsAsync` chỉ trả request của tenant hiện tại.
- `RentalRequestService.GetIncomingRequestsAsync` chỉ trả request thuộc landlord hiện tại.
- `RentalRequestService.GetByIdAsync` trả `null` khi request không tồn tại.
- `RentalRequestService.GetByIdAsync` trả `ForbiddenException` khi user không được xem request.
- `RentalRequestService.ApproveAsync` trả `BadRequestException` khi thiếu payment deadline.
- `RentalRequestService.RejectAsync` chuyển request `Pending` sang `Rejected` khi landlord hợp lệ.
- `RentalRequestService.RejectAsync` trả `BadRequestException` khi lý do từ chối rỗng.
- `RentalRequestService.RejectAsync` trả `ForbiddenException` khi landlord không sở hữu request.
- `RentalRequestService.CancelAsync` chuyển request sang `Cancelled` khi tenant sở hữu request và request còn `Pending`.
- `RentalRequestService.CancelAsync` trả `ForbiddenException` khi tenant không sở hữu request.
- `RentalRequestService.CancelAsync` trả `ConflictException` khi request không còn `Pending`.
- `RoomDepositService.PayAsync` trả `ForbiddenException` khi user thanh toán không phải tenant của deposit.
- `RoomDepositService.PayAsync` trả `ConflictException` khi deposit không ở trạng thái `PendingPayment`.
- `RoomDepositService.PayAsync` trả `null` khi deposit không tồn tại.
- `RoomDepositService.PayAsync` đánh dấu deposit `Paid` và tạo contract khi thanh toán pending deposit thành công.
- `RoomDepositService.PayAsync` trả `ConflictException` khi deposit đã quá hạn.
- `RoomDepositService.PayAsync` trả `ConflictException` khi paid deposit thiếu contract.
- `RoomDepositService.ExpireOverdueAsync` chuyển deposit/request sang `Expired` và trả room `Reserved` về `Available`.

### Invoice / Payment

- `BillingService.GetRoomBillingContextAsync` trả `NotFoundException` khi room không có active contract.
- `BillingService.GetRoomBillingContextAsync` trả billing context khi room có active contract.
- `BillingService.GetRoomInvoicePreviewAsync` validate kỳ hóa đơn không hợp lệ, kỳ vượt tháng và room chưa có active contract.
- `BillingService.GetRoomInvoicePreviewAsync` trả block reason khi thiếu bảng giá dịch vụ.
- `BillingService.GetRoomInvoicePreviewAsync` trả preview có thể generate khi đủ bảng giá dịch vụ.
- `BillingService.GetBillingServiceTypesAsync` chỉ trả service type active và sắp xếp theo tên.
- `BillingService.GetLandlordInvoicesAsync` filter invoice theo landlord/status/search/contract.
- `BillingService.GetLandlordInvoiceAsync` trả `NotFoundException` khi invoice không tồn tại.
- `BillingService.GetLandlordInvoiceAsync` trả `ForbiddenException` khi landlord không sở hữu invoice.
- `BillingService.IssueInvoiceAsync` phát hành invoice `Draft` sang `Issued`.
- `BillingService.IssueInvoiceAsync` trả `BadRequestException` khi invoice không còn là `Draft`.
- `BillingService.CancelInvoiceAsync` chuyển invoice sang `Cancelled` và trim reason.
- `BillingService.CancelInvoiceAsync` trả `BadRequestException` khi invoice đã `Paid`.
- `BillingService.GetMyInvoicesAsync` không trả invoice `Draft`.
- `BillingService.GetMyInvoiceAsync` trả `NotFoundException` khi invoice là `Draft`.
- `BillingService.PayInvoiceAsync` trả invoice cập nhật khi wallet payment thành công.
- `BillingService.PayInvoiceAsync` trả `BadRequestException` khi wallet payment thất bại.
- `BillingService.GenerateInvoiceWithReadingsAsync` validate kỳ hóa đơn, discount âm và danh sách meter reading null.
- `BillingService.GenerateInvoiceWithReadingsAsync` tạo invoice `Draft`, invoice items và meter readings khi input hợp lệ.
- `BillingService.GetTerminationInvoicePreviewAsync` validate contract không tồn tại, thiếu termination date và landlord không sở hữu contract.
- `BillingService.CreateNextTerminationInvoiceAsync` trả `ConflictException` khi contract chưa có termination date.
- `BillingService.CreateFinalInvoiceForTerminationAsync` validate discount âm và meter reading null.
- `InvoiceWalletPaymentService.PayInvoiceAsync` trả `ForbiddenException` khi user thanh toán không phải tenant của invoice.
- `InvoiceWalletPaymentService.PayInvoiceAsync` trả `BadRequestException` khi invoice còn `Draft`.
- `InvoiceWalletPaymentService.PayInvoiceAsync` trả `NotFoundException` khi invoice không tồn tại.
- `InvoiceWalletPaymentService.PayInvoiceAsync` chuyển invoice `Issued` sang `Paid` và lưu transfer group.
- `InvoiceWalletPaymentService.PayInvoiceAsync` idempotent success khi invoice đã `Paid`.

## Integration Tests

### Auth API

- `POST /api/auth/register` trả `409 Conflict` khi email đã tồn tại.
- `POST /api/auth/login` trả `401 Unauthorized` khi user không tồn tại.
- `POST /api/auth/login` trả `403 Forbidden` khi email chưa xác thực.

Lưu ý: base code hiện tại chưa có validation attribute cho email format trong `RegisterRequest`, nên kịch bản “email sai format -> 400” chưa phù hợp với controller/service hiện tại.

### Property API

- `GET /api/rooming-houses/{id}/rooms` trả `200 OK` khi landlord hợp lệ truy cập danh sách room của khu trọ.
- `POST /api/rooming-houses/{id}/rooms` trả `400 BadRequest` khi diện tích phòng không hợp lệ.
- `GET /api/rooms/{id}` trả `404 NotFound` khi room không tồn tại.

### Contract / Deposit API

- `POST /api/rooms/{id}/rental-requests` trả `400 BadRequest` khi ngày bắt đầu thuê quá gần.
- `POST /api/room-deposits/{id}/pay` trả `404 NotFound` khi deposit không tồn tại.

### Invoice / Billing API

- `GET /api/billing/service-types` trả `200 OK`.

## Test Infrastructure Notes

- Unit và integration tests dùng EF Core InMemory để chạy offline.
- Test DbContext tự set `CreatedAt`/`UpdatedAt` vì EF InMemory không thực thi `HasDefaultValueSql("now()")` như PostgreSQL.
- Unit test fixture reset database trước mỗi test instance để tránh dữ liệu test này ảnh hưởng test khác.
- Integration test dùng test authentication scheme qua headers:
  - `X-Test-User-Id`
  - `X-Test-User-Email`
  - `X-Test-User-Roles`
- Integration fixture cấu hình JWT test bằng environment variables để `Program.cs` start được trong `Test` environment.

## Coverage Notes

- JaCoCo phù hợp cho Java; với .NET project này đang dùng `coverlet.collector` và output Cobertura.
- Lệnh đo coverage hiện tại:
  - `dotnet test SmartRentalPlatform.slnx --no-restore --collect:"XPlat Code Coverage" --logger "console;verbosity=minimal" -p:UseSharedCompilation=false -m:1`
- Raw Cobertura total hiện còn thấp vì collector tính cả toàn bộ solution, bao gồm migration/designer/infrastructure/generated code chưa nằm trong scope unit test.
- Coverage theo các service chính đang test hiện nổi bật:
  - `AuthService`: 100% line
  - `InvoiceWalletPaymentService`: 100% line
  - `RoomPriceTierService`: 100% line
  - `RoomCommandService`: ~94% line
  - `RoomDepositService`: ~93% line
  - `KycService`: ~84% line
  - `RentalRequestService`: ~51% line
  - `BillingService`: ~39% line
