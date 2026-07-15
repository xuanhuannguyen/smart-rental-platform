# Báo cáo nghiệp vụ và business logic — Interval 2

**Dự án:** Smart Rental Platform
**Phạm vi:** Interval 2
**Ngày tổng hợp:** 30/06/2026
**Nguồn đối chiếu:** Application services, API controllers, domain states, frontend flows và automated tests

---

## 1. Mục tiêu của interval

Interval 2 mở rộng hệ thống từ quản lý và công khai phòng trọ sang chuỗi nghiệp vụ thuê trọ hoàn chỉnh:

1. Tìm kiếm, gợi ý và xem thông tin phòng.
2. Đặt lịch xem phòng.
3. Gửi và xử lý yêu cầu thuê.
4. Thanh toán đặt cọc và tạo hợp đồng.
5. Hoàn thiện, ký và quản lý hợp đồng điện tử.
6. Quản lý phụ lục, chấm dứt hợp đồng và hóa đơn cuối kỳ.
7. Lập hóa đơn định kỳ và thanh toán bằng ví.
8. Nạp tiền qua PayOS, xử lý webhook và theo dõi giao dịch.
9. Gửi thông báo xuyên suốt các thay đổi nghiệp vụ.

Ngoài các chức năng mới, interval bổ sung regression test cho Auth, KYC, danh mục và quy trình phê duyệt của quản trị viên.

---

## 2. Vai trò tham gia

| Vai trò | Trách nhiệm chính |
|---|---|
| Guest | Tìm kiếm, xem phòng công khai, nhận gợi ý và sử dụng AI chatbot |
| Tenant | Đặt lịch xem, gửi yêu cầu thuê, trả cọc, ký hợp đồng, thanh toán hóa đơn |
| Landlord | Quản lý nhà/phòng, xử lý lịch xem và yêu cầu thuê, lập hóa đơn, ký hợp đồng |
| Admin | Phê duyệt KYC, tài khoản và nhà trọ; theo dõi audit |
| System/Worker | Hết hạn giao dịch, kích hoạt hợp đồng, áp dụng phụ lục và gửi thông báo |
| PayOS | Cung cấp liên kết thanh toán và gửi webhook kết quả nạp tiền |

---

## 3. Chuỗi nghiệp vụ tổng thể

```text
Tìm phòng
   ↓
Đặt lịch xem phòng
   ↓
Gửi yêu cầu thuê
   ↓ landlord duyệt
Phòng được giữ + tạo khoản đặt cọc
   ↓ tenant thanh toán
Tạo hợp đồng nháp
   ↓ khai báo người ở + chốt điều khoản
Landlord ký → Tenant ký
   ↓
Hợp đồng Active
   ↓
Lập hóa đơn → phát hành → thanh toán bằng ví
   ↓
Phụ lục / chấm dứt hợp đồng / hóa đơn cuối kỳ
```

---

## 4. Nghiệp vụ tìm kiếm và gợi ý phòng trọ

### 4.1 Chức năng đã triển khai

- Danh sách nhà trọ và phòng công khai.
- Tìm kiếm có phân trang theo từ khóa và bộ lọc.
- Phân tích câu tìm kiếm tự nhiên thành tiêu chí có cấu trúc.
- Chuẩn hóa tiếng Việt, từ đồng nghĩa và alias tiện ích.
- Lọc theo địa điểm, khoảng cách, giá, tiện ích và thuộc tính phòng.
- Tính khoảng cách địa lý và chấm điểm ứng viên theo rule.
- Gemini có thể làm giàu search intent và rerank kết quả.
- Có phương án rule-based/no-op khi AI không khả dụng.
- Gợi ý nhà trọ cho khách chưa đăng nhập.
- AI chatbot tư vấn dựa trên dữ liệu nhà trọ và ngữ cảnh hội thoại.
- Frontend lưu tìm kiếm gần đây và tín hiệu hành vi thuê để hỗ trợ trải nghiệm.

### 4.2 Logic chính

- Chỉ dữ liệu đủ điều kiện công khai mới xuất hiện trong kết quả public.
- Query được normalize trước khi parse để giảm sai lệch do dấu câu, viết hoa và biến thể từ.
- Alias tiện ích được ánh xạ về amenity chuẩn của hệ thống.
- Geo filter dùng tọa độ và bán kính thay vì so khớp chuỗi địa chỉ đơn thuần.
- Recommendation scorer ưu tiên mức độ phù hợp; AI reranker là lớp bổ sung, không phải điểm lỗi duy nhất.
- Nếu dịch vụ AI lỗi hoặc không cấu hình, tìm kiếm cơ bản vẫn hoạt động.

---

## 5. Quản lý nhà trọ, phòng và bảng giá dịch vụ

### 5.1 Nhà trọ

- Landlord tạo bản nháp nhà trọ và theo dõi onboarding.
- Cập nhật thông tin, tiện ích, hình ảnh, giấy tờ pháp lý và trạng thái hiển thị.
- Gửi hồ sơ nhà trọ để admin xét duyệt.
- Quản lý chính sách thuê, nội quy và bản preview nội quy.
- Quản lý bảng giá dịch vụ theo nhà trọ.

### 5.2 Phòng

- Tạo, xem, cập nhật phòng và hình ảnh.
- Gán tiện ích và bảng giá theo bậc.
- Quản lý trạng thái phòng và gửi phòng để xét duyệt.
- Tra cứu hợp đồng đang active và danh sách người thuê theo phòng.

### 5.3 Business rules

- Diện tích phòng phải lớn hơn `0`.
- Số phòng không được trùng trong cùng một nhà trọ.
- Phòng mới mặc định ở trạng thái `Hidden`.
- Không cho chỉnh sửa các thông tin/bảng giá bị khóa khi phòng đang `Occupied`.
- Khi tạo phòng, nhà trọ phải có đủ bảng giá dịch vụ bắt buộc.
- Price tier được cập nhật theo cơ chế thay thế tập cũ bằng tập mới.
- Landlord chỉ được thao tác trên nhà trọ/phòng thuộc quyền sở hữu của mình.

---

## 6. Nghiệp vụ đặt lịch xem phòng

### 6.1 Chức năng

- Tenant tạo và xem lịch hẹn của mình.
- Landlord xem lịch hẹn, lọc theo trạng thái và kiểm tra xung đột.
- Landlord xác nhận, từ chối hoặc đề xuất thời gian khác.
- Tenant chấp nhận hoặc từ chối thời gian được đề xuất.
- Tenant và landlord đều có thể hủy theo đúng quyền.
- Landlord đánh dấu lịch xem đã hoàn thành.

### 6.2 Vòng đời

```text
Pending → Confirmed → Completed
   ├──→ Rejected
   ├──→ CancelledByTenant
   ├──→ CancelledByLandlord
   └──→ Expired
```

Khi landlord từ chối kèm giờ đề xuất, tenant có thể chấp nhận để tiếp tục lịch hẹn hoặc từ chối đề xuất.

---

## 7. Yêu cầu thuê phòng

### 7.1 Chức năng

- Tenant gửi yêu cầu thuê từ một phòng cụ thể.
- Tenant xem danh sách và chi tiết yêu cầu của mình.
- Landlord xem các yêu cầu đến.
- Landlord duyệt hoặc từ chối.
- Tenant hủy yêu cầu khi còn chờ xử lý.

### 7.2 Business rules đã triển khai

- Phòng phải tồn tại và đang có thể cho thuê.
- Nhà trọ phải có rental policy trước khi nhận yêu cầu.
- Landlord không được tự thuê phòng của chính mình.
- Ngày bắt đầu thuê phải đáp ứng khoảng thời gian báo trước.
- Số người ở không được vượt sức chứa của phòng.
- Một tenant không được có hai yêu cầu `Pending` cho cùng phòng.
- Chỉ landlord sở hữu phòng mới được duyệt/từ chối yêu cầu.
- Lý do từ chối không được để trống.
- Chỉ tenant sở hữu request mới được hủy.
- Chỉ request `Pending` mới được hủy hoặc xử lý.
- Khi duyệt phải có hạn thanh toán đặt cọc hợp lệ.

### 7.3 Kết quả khi duyệt

- Request chuyển sang trạng thái được duyệt.
- Phòng được giữ chỗ (`Reserved`).
- Hệ thống tạo khoản đặt cọc ở trạng thái `PendingPayment`.
- Tenant nhận thông tin hạn thanh toán.

---

## 8. Đặt cọc và khởi tạo hợp đồng

### 8.1 Business rules

- Chỉ tenant gắn với deposit mới được thanh toán.
- Deposit phải ở trạng thái `PendingPayment`.
- Deposit quá hạn không thể thanh toán.
- Thanh toán thành công chuyển deposit sang `Paid`.
- Sau khi trả cọc, hệ thống tạo hợp đồng tương ứng.
- Nếu deposit đã `Paid` nhưng thiếu contract, hệ thống báo xung đột dữ liệu thay vì tạo trùng mù quáng.

### 8.2 Xử lý hết hạn

Worker `ExpireOverdueAsync`:

- Chuyển deposit quá hạn sang `Expired`.
- Cập nhật request liên quan sang trạng thái hết hạn.
- Trả phòng từ `Reserved` về `Available`.

### 8.3 Trạng thái deposit

`PendingPayment`, `Paid`, `Refunded`, `Forfeited`, `Cancelled`, `Expired`.

---

## 9. Hợp đồng thuê điện tử

### 9.1 Chức năng

- Tenant và landlord xem lịch sử/danh sách hợp đồng.
- Xem chi tiết và preview PDF.
- Tenant khai báo người cùng ở và giấy tờ liên quan.
- Landlord cập nhật điều khoản hợp đồng.
- Hai bên yêu cầu OTP và ký hợp đồng.
- Lưu thông tin chữ ký, địa chỉ IP và user agent.
- Một bên có thể yêu cầu sửa đổi hoặc từ chối.
- Sinh, liệt kê và tải file hợp đồng.
- Tra cứu hợp đồng active và người thuê theo phòng.
- Worker hết hạn hợp đồng quá hạn ký.
- Worker kích hoạt hợp đồng đến ngày nhận phòng.

### 9.2 Vòng đời hợp đồng

```text
WaitingTenantOccupants
  → PendingLandlordSignature
  → PendingTenantSignature
  → Active
```

Các nhánh ngoại lệ:

- `LandlordRevisionRequested`
- `TenantRevisionRequested`
- `Rejected`
- `Cancelled`
- `Expired`

### 9.3 Logic ký

- Chỉ đúng bên ký và đúng trạng thái mới được ký.
- OTP được yêu cầu và xác minh trước khi ghi nhận chữ ký.
- Landlord ký trước, tenant ký sau theo workflow hiện tại.
- Thông tin ký được lưu để phục vụ truy vết.
- Hợp đồng hoàn tất chữ ký nhưng chưa đến ngày bắt đầu được worker kích hoạt đúng thời điểm.

---

## 10. Phụ lục và thay đổi hợp đồng

### 10.1 Chức năng

- Tạo, xem, cập nhật và xóa phụ lục.
- Preview phụ lục dạng PDF.
- Yêu cầu OTP và ký phụ lục.
- Từ chối hoặc yêu cầu chỉnh sửa.
- Worker tự áp dụng phụ lục đến ngày có hiệu lực.

### 10.2 Trạng thái

`Draft`, `PendingSignature`, `Active`, `Rejected`, `Cancelled`, `LandlordRevisionRequested`, `TenantRevisionRequested`.

### 10.3 Logic

- Chỉ các bên thuộc hợp đồng mới được truy cập phụ lục.
- Phụ lục nháp có thể chỉnh sửa/xóa theo quyền và trạng thái.
- Chữ ký phụ lục sử dụng OTP và audit metadata tương tự hợp đồng.
- Thay đổi chưa đến ngày hiệu lực không được áp dụng sớm.
- Worker áp dụng các phụ lục hợp lệ khi đến hạn.

---

## 11. Chấm dứt hợp đồng

- Tenant hoặc landlord gửi yêu cầu chấm dứt theo quyền và trạng thái hợp đồng.
- Hệ thống lưu loại chấm dứt, ngày hiệu lực và lý do.
- Landlord xem preview hóa đơn chấm dứt.
- Có thể tạo hóa đơn tiếp theo hoặc hóa đơn cuối cùng cho hợp đồng.
- Hóa đơn cuối kỳ hỗ trợ chỉ số công tơ và giảm trừ.
- Không cho tạo quy trình chấm dứt nếu thiếu ngày chấm dứt cần thiết.
- Chỉ landlord sở hữu hợp đồng mới được lập hóa đơn chấm dứt.

---

## 12. Billing và hóa đơn

### 12.1 Chức năng

- Lấy billing context theo phòng/hợp đồng active.
- Preview hóa đơn trước khi tạo.
- Lập hóa đơn từ chỉ số điện/nước và bảng giá dịch vụ.
- Quản lý danh sách hóa đơn của landlord.
- Phát hành hoặc hủy hóa đơn.
- Tenant xem hóa đơn của mình/theo hợp đồng.
- Tenant thanh toán hóa đơn bằng ví.
- Lập preview và hóa đơn chấm dứt hợp đồng.

### 12.2 Vòng đời hóa đơn

```text
Draft → Issued → Paid
            └──→ Overdue
Draft/Issued ──→ Cancelled (theo rule)
```

### 12.3 Business rules

- Phòng phải có hợp đồng active để lập hóa đơn thông thường.
- Ngày kết thúc kỳ không được trước ngày bắt đầu.
- Kỳ hóa đơn không được vượt phạm vi tháng cho phép.
- Phải có đủ bảng giá dịch vụ; nếu thiếu, preview trả block reason.
- Discount không được âm.
- Danh sách meter reading không được `null`.
- Hóa đơn mới được tạo ở trạng thái `Draft`.
- Chỉ `Draft` mới được phát hành.
- Không được hủy hóa đơn đã `Paid`.
- Tenant không nhìn thấy hóa đơn `Draft`.
- Tenant chỉ thanh toán hóa đơn của chính mình.
- Thanh toán thành công chuyển `Issued` sang `Paid` và lưu transfer group.
- Thanh toán lại invoice đã `Paid` trả idempotent success, không trừ tiền lần hai.

---

## 13. Ví và giao dịch nội bộ

### 13.1 Chức năng

- Tự tạo ví khi người dùng lần đầu truy cập.
- Xem số dư và lịch sử giao dịch có phân trang.
- Credit, debit, reserve và release reserved balance.
- Chuyển tiền giữa hai ví.
- Chuyển tiền vào/ra số dư reserve trong cùng database transaction.
- Gắn metadata và mã nhóm để đối soát giao dịch liên quan.

### 13.2 Business rules

- Số tiền giao dịch phải dương.
- Không cho debit hoặc reserve vượt số dư khả dụng.
- Không cho release vượt reserved balance.
- Các bước chuyển tiền hai chiều chạy trong transaction.
- Debit và credit của cùng một lần chuyển dùng chung transfer group.
- Trạng thái giao dịch gồm `Succeeded`, `Failed`, `Reversed`.
- Các thao tác thanh toán cần hỗ trợ idempotency và kiểm soát concurrent update.

---

## 14. Nạp tiền PayOS và webhook

### 14.1 Luồng nạp tiền

1. User nhập số tiền.
2. Hệ thống tạo `PaymentTransaction` trạng thái `Pending`.
3. Tạo order/payment link qua PayOS.
4. PayOS gửi webhook.
5. Hệ thống kiểm tra chữ ký và đối chiếu giao dịch.
6. Nếu thành công, ví được credit đúng một lần.
7. User xem kết quả và lịch sử nạp.

### 14.2 Business rules

- Validate số tiền và dữ liệu yêu cầu trước khi tạo payment.
- User chỉ xem được top-up của mình.
- Webhook PayOS phải qua bước xác minh chữ ký.
- Lưu webhook log để audit trạng thái xử lý.
- Webhook lặp không được cộng tiền lần hai.
- Giao dịch pending quá hạn được worker chuyển sang `Expired`.
- Trạng thái payment: `Pending`, `Succeeded`, `Failed`, `Expired`, `Cancelled`.
- Có mock payment endpoint cho môi trường phát triển; không phải luồng production.

---

## 15. Thông báo

### 15.1 Chức năng

- Tạo thông báo kèm loại và reference đến nghiệp vụ nguồn.
- Xem danh sách mới nhất theo giới hạn.
- Đếm chưa đọc.
- Đánh dấu một hoặc tất cả là đã đọc.
- Xóa thông báo theo quyền sở hữu.
- Frontend có notification bell, badge chưa đọc và trang lịch sử.

### 15.2 Business rules

- Notification mới mặc định là chưa đọc.
- User chỉ đọc, cập nhật hoặc xóa notification của mình.
- Danh sách được sắp theo thời gian giảm dần.
- Thao tác `mark all` chỉ tác động notification chưa đọc của user hiện tại.
- Reference được dùng để điều hướng về request, invoice, contract hoặc nghiệp vụ liên quan.

---

## 16. Nghiệp vụ nền được củng cố trong interval

### 16.1 Auth

- Đăng ký tenant, gán role và tạo OTP xác thực email.
- Không cho đăng ký email trùng.
- Đăng nhập trả access token và refresh token.
- Chặn tài khoản bị ban hoặc chưa xác thực email.
- Khóa tài khoản sau nhiều lần nhập sai mật khẩu.

### 16.2 KYC

- Kiểm tra đủ file bắt buộc và loại giấy tờ hợp lệ.
- Chặn citizen ID đã được duyệt cho người khác.
- Lưu hồ sơ và chuyển sang `PendingAdminReview`.
- Xem trạng thái mới nhất và lịch sử theo thời gian giảm dần.

### 16.3 Admin approval

- Phê duyệt/từ chối KYC và nhà trọ.
- Quản lý trạng thái người dùng.
- Lưu approval audit để truy vết hành động quản trị.

### 16.4 Danh mục

- Chỉ trả tỉnh/phường đang active và sắp xếp theo tên.
- Lọc tiện ích theo scope `House`, `Room` hoặc `Both`.

---

## 17. Background workers

| Worker | Trách nhiệm |
|---|---|
| Payment transaction expiration | Hết hạn top-up pending quá thời gian |
| Room deposit expiration | Hết hạn đặt cọc, request và giải phóng phòng |
| Rental contract expiration | Hết hạn hợp đồng quá hạn ký |
| Move-in activation | Kích hoạt hợp đồng đủ chữ ký khi đến ngày bắt đầu |
| Contract appendix application | Áp dụng phụ lục đã ký khi đến ngày hiệu lực |

Workers giúp chuyển trạng thái theo thời gian mà không phụ thuộc người dùng mở màn hình.

---

## 18. Frontend flows đã hoàn thiện

- Trang tìm kiếm, bộ lọc địa điểm và gợi ý phòng.
- Public rooming-house/room detail.
- AI chatbot tư vấn thuê trọ.
- Tenant và landlord quản lý lịch xem phòng.
- Tenant gửi/theo dõi yêu cầu thuê; landlord xử lý yêu cầu đến.
- Thiết lập người ở và điều khoản hợp đồng.
- Preview, ký và quản lý hợp đồng.
- Tạo/xem/ký phụ lục; chấm dứt hợp đồng.
- Landlord lập, phát hành và quản lý hóa đơn.
- Tenant xem và thanh toán hóa đơn.
- Xem ví, nạp tiền, kết quả top-up và lịch sử giao dịch.
- Notification bell và trang thông báo.
- Landlord chỉnh sửa phòng, nhà trọ và bảng giá dịch vụ.

---

## 19. Kiểm thử và chất lượng

### 19.1 Kết quả tự động

| Nhóm | Kết quả |
|---|---:|
| Unit tests | 157/157 pass |
| Integration tests | 9/9 pass |
| Tổng | **166/166 pass** |
| Backend Release build | Pass, 0 error |
| Frontend production build | Pass |

### 19.2 Phạm vi được test nổi bật

- Auth và KYC.
- Admin approval và audit.
- Tạo/cập nhật phòng, price tier.
- Rental request và room deposit.
- Billing và thanh toán invoice bằng ví.
- Wallet credit/debit/reserve/transfer.
- PayOS top-up, webhook và idempotency.
- Notification.
- Search parser, geo helper và recommendation scorer.
- Một số API Auth, Property, Rental Request, Deposit và Billing.

### 19.3 Giới hạn kiểm thử hiện tại

- Hợp đồng, chữ ký OTP, phụ lục và lịch xem phòng đã có implementation/API nhưng chưa có test suite chuyên sâu tương đương Billing hoặc Rental Request.
- Integration tests đang dùng EF Core InMemory, chưa thay thế hoàn toàn test với PostgreSQL thật.
- Chưa có end-to-end test tự động bao phủ toàn bộ hành trình tenant–landlord.
- Build hiện còn warning về nullable, API obsolete và raw SQL trong seeder; không có build error.
- Frontend bundle còn lớn và cần code splitting ở giai đoạn tối ưu.

---

## 20. Kết luận

Interval 2 đã hình thành được chuỗi nghiệp vụ thuê trọ cốt lõi từ tìm kiếm đến vận hành hợp đồng và thanh toán. Các module Rental Request, Deposit, Billing, Wallet, Payment và Notification đã có automated test tương đối rõ. Hợp đồng điện tử, phụ lục, lịch xem phòng và AI search đã triển khai đầy đủ bề mặt service/API/frontend, nhưng nên được ưu tiên bổ sung integration/E2E test trong interval tiếp theo.

Về trạng thái kỹ thuật, solution hiện build thành công và toàn bộ `166` automated tests đều pass. Các cấu hình bí mật trong appsettings không thuộc nội dung báo cáo hoặc thay đổi cần đưa lên repository.
