# Smart Rental Platform - Kịch Bản Demo Tổng Thể

Tài liệu này mô tả kịch bản demo chuẩn để trình bày các chức năng chính của hệ thống theo một mạch liền mạch. Mục tiêu là seed data sao cho mỗi vai người dùng có một câu chuyện rõ ràng, thao tác liên tục và thể hiện được giá trị sản phẩm.

## Nguyên Tắc Seed Data Demo

- Không đặt tên public có chữ `demo`, `mock`, `test`, hoặc mã phòng kiểu `#123`.
- Tên khu trọ, phòng, người dùng, bình luận, tin nhắn phải tự nhiên như dữ liệu thật.
- Ảnh khu trọ, phòng, review, công tơ, hợp đồng dùng ảnh thật trên S3, không dùng SVG placeholder.
- Mỗi khu trọ cần có 3-5 ảnh, mỗi phòng 3-5 ảnh, 5 review, 3-5 review có chủ trọ phản hồi.
- Dữ liệu giữa các luồng phải liên kết: guest tìm thấy khu trọ, tenant đặt lịch, chủ trọ phản hồi, tenant thuê phòng, hợp đồng sinh hóa đơn, kết thúc hợp đồng mới được review.
- Các tài khoản demo nên dùng chung mật khẩu để thao tác nhanh: `Demo@123456`.

## Tổng Quan Vai Demo

| Vai | Mục tiêu trình bày | Tài khoản gợi ý |
| --- | --- | --- |
| Người 1 | Guest tìm kiếm, filter, chatbot, yêu thích, đăng nhập, chat, lịch xem, yêu cầu thuê, eKYC, ký hợp đồng, ký phụ lục | `nguyenxuanhuan.dev@gmail.com` |
| Người 2 | Tenant đã có hợp đồng active, hóa đơn, AI đọc công tơ, nạp tiền, thanh toán, hủy hợp đồng, hóa đơn kỳ cuối, review sau kết thúc | `hoctienganh4english@gmail.com` |
| Người 3 | Tài khoản đã KYC, đăng ký làm chủ trọ, đăng khu trọ đầu tiên, admin duyệt | `pham.ngoc.mai@example.com` |
| Người 4 | Chủ trọ trung tâm của luồng 1 và luồng 2, có khu trọ public, lịch xem, hợp đồng active, hóa đơn và dashboard quản lý | `nguyenxuanhuan21102005@gmail.com` |
| Người 5 | AI kiểm duyệt review, admin xử lý review/khiếu nại, admin quản lý tiện ích/tỉnh thành, tổng kết demo | `admin.demo@example.com` |

## Liên Kết Dữ Liệu Chính Giữa Các Luồng

Chủ trọ trung tâm của luồng 1 và luồng 2 là cùng một người: `nguyenxuanhuan21102005@gmail.com`.

- Ở luồng 1, guest/tenant mới tìm thấy khu trọ của chủ trọ này, gửi lịch xem phòng, nhắn tin và gửi yêu cầu thuê.
- Ở luồng 2, tenant `hoctienganh4english@gmail.com` đang có hợp đồng active với chính chủ trọ này.
- Cùng một landlord sẽ có dữ liệu xuyên suốt: khu trọ public, phòng trống, phòng đang thuê, lịch xem, chat, hợp đồng, hóa đơn, phụ lục, review.

## Luồng 1 - Guest Đến Tenant Mới Thuê Phòng

### Mục Tiêu

Trình bày luồng người dùng mới từ lúc chưa đăng nhập đến khi xác thực eKYC, gửi yêu cầu thuê, ký hợp đồng và ký phụ lục.

### Bước Demo

1. Vào trang home với trạng thái guest.
2. Tìm kiếm khu trọ theo tên hoặc khu vực.
3. Dùng filter giá, diện tích, tiện ích, khu vực.
4. Mở chatbot tư vấn, hỏi gợi ý khu trọ theo nhu cầu.
5. Bấm thêm yêu thích một khu trọ.
6. Hệ thống yêu cầu đăng nhập vì guest không được thêm yêu thích.
7. Bấm nhắn tin với chủ trọ.
8. Hệ thống yêu cầu đăng nhập vì guest không được chat.
9. Đăng nhập tài khoản `nguyenxuanhuan.dev@gmail.com`.
10. Vào lại khu trọ vừa quan tâm, thêm yêu thích thành công.
11. Gửi lịch xem phòng tới chủ trọ `nguyenxuanhuan21102005@gmail.com`.
12. Chủ trọ `nguyenxuanhuan21102005@gmail.com` nhận thông báo và phản hồi lịch xem phòng.
13. Tenant nhắn tin với chủ trọ này về giá, tiện ích, giờ xem phòng.
14. Chủ trọ `nguyenxuanhuan21102005@gmail.com` phản hồi trong chat.
15. Tenant gửi yêu cầu thuê phòng.
16. Hệ thống chặn thao tác nếu chưa eKYC.
17. Tenant vào `/me/kyc`, upload CCCD mặt trước, mặt sau, selfie.
18. Gửi eKYC VNPT.
19. Hồ sơ vào `PendingAdminReview`.
20. Admin duyệt KYC.
21. Tenant gửi lại yêu cầu thuê phòng.
22. Chủ trọ `nguyenxuanhuan21102005@gmail.com` chấp nhận yêu cầu.
23. Hệ thống tạo hợp đồng.
24. Tenant ký hợp đồng lần 1.
25. Chủ trọ `nguyenxuanhuan21102005@gmail.com` ký hợp đồng.
26. Hợp đồng active.
27. Chủ trọ `nguyenxuanhuan21102005@gmail.com` tạo phụ lục thay đổi thông tin hợp đồng, ví dụ: thêm người ở, điều chỉnh giá phòng, cập nhật điều khoản.
28. Tenant xem phụ lục, ký phụ lục.
29. Chủ trọ `nguyenxuanhuan21102005@gmail.com` ký phụ lục.
30. Mở màn chi tiết hợp đồng để xem hợp đồng gốc, phụ lục, lịch sử ký, file hợp đồng.

### Seed Data Cần Có

- Tài khoản `nguyenxuanhuan.dev@gmail.com` có email confirmed nhưng chưa có KYC approved.
- Ít nhất một khu trọ của `nguyenxuanhuan21102005@gmail.com` có phòng trống, ảnh thật, review tốt, chủ trọ online.
- Một lịch xem phòng pending/confirmed để show lịch sử sau khi tạo.
- Chủ trọ của khu trọ là `nguyenxuanhuan21102005@gmail.com`, tài khoản landlord active và KYC approved.
- VNPT eKYC cấu hình real hoặc fallback cho phép admin duyệt nếu provider lỗi.
- Mẫu hợp đồng có thể ký, có template phụ lục, có file hợp đồng render.

## Luồng 2 - Tenant Đang Thuê, Hóa Đơn, Hủy Hợp Đồng, Review

### Mục Tiêu

Trình bày vòng đời tenant đã có hợp đồng active: xem hợp đồng, xem hóa đơn, ảnh AI đồng hồ điện nước, thanh toán, hủy hợp đồng, hóa đơn kỳ cuối, điều kiện thuê tiếp và review sau khi kết thúc.

### Bước Demo

1. Đăng nhập tài khoản `hoctienganh4english@gmail.com`.
2. Vào lịch sử thuê/hợp đồng hiện tại với chủ trọ `nguyenxuanhuan21102005@gmail.com`.
3. Mở hợp đồng active trong tháng hiện tại.
4. Xem chi tiết hợp đồng, người ở, file hợp đồng, chữ ký, phụ lục.
5. Vào hóa đơn.
6. Xem hóa đơn các tháng trước đã thanh toán, ưu tiên hóa đơn tháng 06/2026.
7. Mở hóa đơn cũ để xem item tiền phòng, điện, nước, dịch vụ.
8. Xem ảnh đồng hồ điện nước đã seed cho AI OCR.
9. Xem hóa đơn hiện tại tháng 07/2026 đang `Issued/Unpaid`.
10. Mở ví tenant để thấy ví còn đủ tiền thanh toán, nhưng hợp đồng vẫn đang giữ cọc riêng.
11. Thử hủy hợp đồng khi hóa đơn hiện tại chưa thanh toán.
12. Hệ thống báo phải thanh toán hóa đơn đang nợ trước.
13. Thanh toán hóa đơn hiện tại.
14. Quay lại hợp đồng, gửi yêu cầu hủy hợp đồng trước hạn.
15. Mở phần tiền cọc/hợp đồng đang giữ: cọc 1 tháng đang nằm ở trạng thái giữ trên ví chủ trọ.
16. Vì tenant hủy trước hạn/vi phạm điều khoản hợp đồng, hệ thống không hoàn cọc cho tenant.
17. Khoản cọc đang giữ được tất toán chuyển vào ví khả dụng của chủ trọ `nguyenxuanhuan21102005@gmail.com`.
18. Chủ trọ `nguyenxuanhuan21102005@gmail.com` tạo hóa đơn kỳ cuối sau khi chấp nhận hủy.
19. Tenant thử thuê phòng mới khi hóa đơn kỳ cuối chưa thanh toán.
20. Hệ thống chặn vì còn công nợ hợp đồng cũ.
21. Tenant thanh toán hóa đơn kỳ cuối.
22. Hợp đồng kết thúc hoàn toàn.
23. Tenant có thể gửi yêu cầu thuê phòng mới.
24. Tenant đánh giá khu trọ cũ.
25. AI review nội dung bình luận.
26. Nếu nội dung tốt, review được approved và hiển thị public.
27. Nếu nội dung có rủi ro, review vào hàng chờ admin review.

### Seed Data Cần Có

- Tài khoản `hoctienganh4english@gmail.com` KYC approved.
- Hợp đồng active đã ký đầy đủ giữa `hoctienganh4english@gmail.com` và chủ trọ `nguyenxuanhuan21102005@gmail.com`.
- Hợp đồng giống seed chuẩn của `feat/draft-interval3`: phòng `B201`, khu trọ `Khu trọ Xuân Huân`, mã hợp đồng `HD-XH-B201-20260601`.
- Tiền phòng 1 người: `3.600.000 đ/tháng`.
- Tiền cọc hợp đồng: `3.600.000 đ`, trạng thái `Paid`, đang được giữ ở ví chủ trọ.
- Ví chủ trọ có `reserved_balance = 3.600.000 đ` trước khi hủy hợp đồng.
- Khi tenant hủy trước hạn/vi phạm hợp đồng, cọc `3.600.000 đ` bị forfeited và chuyển từ số dư giữ sang số dư khả dụng của chủ trọ.
- Ít nhất 3 hóa đơn tháng trước đã thanh toán hoặc lịch sử hóa đơn đủ để dashboard có doanh thu.
- Một hóa đơn hiện tại tháng 07/2026 `Issued/Unpaid`, tổng tiền khoảng `4.228.000 đ`.
- Ảnh công tơ điện/nước gắn với meter readings.
- Chỉ số điện demo: `1250 -> 1341`, tiêu thụ `91 kWh`.
- Chỉ số nước demo: `88 -> 96`, tiêu thụ `8 m3`.
- Ví tenant có đủ tiền để thanh toán hóa đơn hiện tại khi demo.
- Một trạng thái công nợ để chặn hủy hợp đồng/thuê tiếp.
- Một flow hóa đơn kỳ cuối `Draft/Issued`, ví dụ phí vệ sinh kỳ cuối `80.000 đ`.
- Hợp đồng ended để cho phép review sau khi thanh toán hết.

## Luồng 3 - Đăng Ký Làm Chủ Trọ Và Đăng Khu Trọ Đầu Tiên

### Mục Tiêu

Trình bày người dùng đã KYC đăng ký làm chủ trọ, tạo khu trọ đầu tiên, upload ảnh, viết luật, admin duyệt và khu trọ hiển thị public.

### Bước Demo

1. Đăng nhập tài khoản `pham.ngoc.mai@example.com`.
2. Bấm đăng ký làm chủ trọ.
3. Hệ thống cho phép vì đã eKYC.
4. Tạo khu trọ mới.
5. Nhập tên khu trọ tự nhiên, địa chỉ, tỉnh/thành, phường/xã.
6. Upload ảnh khu trọ thật.
7. Tạo 5-8 phòng.
8. Upload ảnh từng phòng.
9. Cấu hình giá phòng, diện tích, sức chứa, tiện ích.
10. Viết luật khu trọ: giờ giấc, an ninh, vệ sinh, khách đến, xe, điện nước, bồi thường.
11. Gửi hồ sơ khu trọ cho admin duyệt.
12. Admin vào màn pending approval.
13. Admin xem ảnh, thông tin pháp lý, luật khu trọ.
14. Admin duyệt.
15. Khu trọ hiển thị trên trang public.

### Seed Data Cần Có

- Tài khoản `pham.ngoc.mai@example.com`.
- Tên hiển thị: `Phạm Ngọc Mai`.
- Mật khẩu chung: `Demo@123456`.
- Trạng thái account: active, email confirmed, onboarding completed.
- KYC: approved bằng VNPT/eKYC seed sẵn, CCCD masked hợp lệ, không cần duyệt lại.
- Vai trò ban đầu: tenant đã KYC; khi demo bấm đăng ký làm chủ trọ thì hệ thống nâng vai hoặc tạo landlord profile.
- Dữ liệu địa chỉ hợp lệ theo administrative catalog.
- Bộ ảnh khu trọ/phòng thật trên S3.
- Một hồ sơ khu trọ pending để admin duyệt ngay sau khi người dùng gửi.
- Khu trọ đề xuất cho luồng 3: `Khu trọ An Nhiên`.
- Địa chỉ đề xuất: khu vực gần trường đại học tại Đà Nẵng hoặc Cần Thơ, không dùng tên demo.
- 5 phòng đầu tiên: `A01`, `A02`, `A03`, `B01`, `B02`.
- Mỗi phòng có 3-5 ảnh thật, giá 2.800.000-4.200.000 đ/tháng, diện tích 18-28 m2.
- Luật khu trọ đầy đủ: giờ yên tĩnh, khách qua đêm, gửi xe, vệ sinh, điện nước, bồi thường.
- Sau approve, khu trọ visible và searchable.

## Luồng 4 - Dashboard Chủ Trọ, Hóa Đơn Hàng Loạt, Rút Tiền, Group Chat

### Mục Tiêu

Trình bày năng lực quản lý của chủ trọ đã có nhiều khu trọ, phòng đang thuê, doanh thu, hóa đơn, rút tiền và nhắn tin nhóm.

### Bước Demo

1. Đăng nhập landlord có sẵn dữ liệu, ví dụ `xunhuns21@gmail.com`.
2. Mở dashboard chủ trọ.
3. Xem doanh thu theo tháng, số phòng đang thuê, phòng trống, lịch xem phòng.
4. Xem danh sách khu trọ/phòng.
5. Vào khu trọ có nhiều phòng đang thuê.
6. Điều chỉnh giá điện, nước, internet, rác.
7. Chọn tạo hóa đơn hàng loạt.
8. Upload/chọn ảnh đồng hồ điện nước đã seed.
9. AI OCR đọc chỉ số điện nước.
10. Hệ thống tạo preview hóa đơn hàng loạt.
11. Chủ trọ kiểm tra và phát hành hóa đơn.
12. Vào ví chủ trọ.
13. Xem tiền đã nhận từ tenant.
14. Tạo yêu cầu rút tiền.
15. Xem trạng thái rút tiền pending/admin approved.
16. Vào chat.
17. Tạo group chat theo khu trọ.
18. Mời các tenant trong khu trọ vào nhóm.
19. Gửi tin nhắn, gửi ảnh, gửi file.
20. Cấp quyền admin nhóm cho một thành viên.
21. Xóa/đổi quyền thành viên để show quản lý nhóm.

### Seed Data Cần Có

- Landlord có ít nhất 2 khu trọ.
- Mỗi khu trọ có phòng đang thuê, phòng trống, lịch xem phòng pending/confirmed/completed.
- Hóa đơn các tháng trước đã thanh toán.
- Meter readings có ảnh công tơ.
- Wallet landlord có số dư và lịch sử giao dịch.
- Withdrawal request pending và approved.
- Group chat có thành viên, tin nhắn, ảnh, file, unread count.

## Luồng 5 - Review Moderation, Admin Catalog, Tổng Kết

### Mục Tiêu

Trình bày AI moderation và vai trò admin trong việc kiểm soát nội dung, tiện ích, tỉnh thành, chất lượng dữ liệu.

### Bước Demo

1. Đăng nhập tenant đã kết thúc hợp đồng.
2. Gửi review tốt cho khu trọ.
3. AI duyệt review và hiển thị public.
4. Gửi review có nội dung không phù hợp.
5. AI gắn risk cao và chuyển sang admin review.
6. Admin đăng nhập.
7. Admin vào danh sách review cần duyệt.
8. Admin xem nội dung, lý do AI đánh dấu, ảnh review nếu có.
9. Admin approve/reject.
10. Tenant/người dùng kháng cáo một review bị xử lý.
11. Admin xem kháng cáo và ra quyết định.
12. Admin vào catalog tiện ích.
13. Thêm/sửa tiện ích, ví dụ: máy giặt, camera an ninh, chỗ để xe.
14. Admin vào quản lý tỉnh thành/phường xã.
15. Thêm/sửa/kích hoạt/vô hiệu hóa khu vực.
16. Kết demo bằng dashboard/tổng kết chức năng và các điểm cần phát huy.

### Seed Data Cần Có

- Review approved có chủ trọ phản hồi.
- Review pending AI/admin review.
- Review bị report/khiếu nại.
- Ảnh review thật hoặc ảnh phòng liên quan.
- Catalog amenities có sẵn nhiều tiện ích.
- Administrative data có đủ tỉnh/thành, phường/xã.

## Kịch Bản Kết Demo

Kết thúc demo nên tóm tắt các điểm mạnh:

- Tìm kiếm và gợi ý phòng trọ theo nhu cầu.
- Luồng guest đến tenant có login, yêu thích, chat, lịch xem phòng.
- eKYC và admin approval giúp tăng độ tin cậy.
- Hợp đồng, ký số, phụ lục và lịch sử hợp đồng đầy đủ.
- Billing có hóa đơn, ví, thanh toán, công nợ, hóa đơn kỳ cuối.
- AI đọc đồng hồ điện nước và hỗ trợ tạo hóa đơn hàng loạt.
- Chủ trọ có dashboard, doanh thu, phòng, lịch xem, rút tiền.
- Chat 1-1 và group chat có file/ảnh/quyền admin.
- Review có AI moderation và admin review.
- Admin quản trị catalog, địa giới, KYC, khu trọ, review.

## Checklist Seed Data Chuẩn Bị Demo

- [ ] 1 account guest-to-tenant chưa KYC: `nguyenxuanhuan.dev@gmail.com`.
- [ ] 1 account tenant đã active contract: `hoctienganh4english@gmail.com`.
- [ ] 1 account landlord mới/đã KYC để tạo khu trọ đầu tiên.
- [ ] 1 account landlord có sẵn dashboard đầy đủ: `xunhuns21@gmail.com`.
- [ ] 1 account admin: `admin.demo@example.com`.
- [ ] 10-20 tỉnh/thành có nhiều trường đại học.
- [ ] Khoảng 500 khu trọ public, ảnh thật S3, không SVG.
- [ ] Mỗi khu trọ có 5-8 phòng.
- [ ] Mỗi khu trọ có 5 review, 3-5 landlord replies.
- [ ] Một tập hợp hợp đồng active, ended, pending signature, appendix pending.
- [ ] Hóa đơn paid/unpaid/final invoice/công nợ.
- [ ] Meter readings có ảnh đồng hồ điện nước thật.
- [ ] Wallet topup/payment/withdrawal transactions.
- [ ] Direct chat và group chat có tin nhắn, file, ảnh.
- [ ] Review AI approved, AI flagged, admin reviewed, appealed.

## Data Sẽ Seed Thêm Chờ Duyệt

Phần này là đề xuất seed thêm trước khi sửa code seed. Mục tiêu là khớp lại mạch demo với dữ liệu chuẩn của `feat/draft-interval3`, nhưng bỏ chữ public kiểu `demo`, không dùng ảnh SVG.

### Gói A - Luồng 2 Active Contract Chuẩn

- Tenant: `hoctienganh4english@gmail.com`, tên `Lê Quang Linh`, KYC approved.
- Landlord: `nguyenxuanhuan21102005@gmail.com`, tên `Nguyễn Xuân Huân - Chủ trọ Xuân Huân`, KYC approved.
- Khu trọ: `Khu trọ Xuân Huân`.
- Phòng active: `B201`, trạng thái `Occupied`, tối đa 2 người.
- Hợp đồng active: mã hợp đồng `HD-XH-B201-20260601`, ngày bắt đầu `01/06/2026`, ngày kết thúc `31/05/2027`.
- Tiền thuê: `3.600.000 đ/tháng` cho 1 người, tier 2 người `3.950.000 đ/tháng`.
- Tiền cọc: `3.600.000 đ`, đã thanh toán, đang giữ ở ví chủ trọ.
- Ví tenant: đủ tiền để thanh toán hóa đơn hiện tại.
- Ví chủ trọ: có `reserved_balance = 3.600.000 đ` trước khi hủy; sau hủy trước hạn, cọc chuyển sang balance khả dụng của chủ trọ.
- Hóa đơn đã thanh toán: ít nhất tháng 06/2026, tổng khoảng `4.280.000 đ`.
- Hóa đơn hiện tại: tháng 07/2026, `Issued/Unpaid`, tổng khoảng `4.228.000 đ`.
- Meter readings: điện `1250 -> 1341`, nước `88 -> 96`, ảnh công tơ thật trên S3.
- Hóa đơn kỳ cuối: phí vệ sinh/khấu trừ cuối kỳ `80.000 đ`, dùng để chặn thuê tiếp cho tới khi thanh toán.
- File hợp đồng: preview PDF và signed legal PDF theo luồng VNPT eContract.
- Chữ ký: tenant và landlord đều `Signed`, có provider evidence VNPT.

### Gói B - Luồng 3 Đăng Ký Chủ Trọ

- Account thao tác: `pham.ngoc.mai@example.com`.
- Tên hiển thị: `Phạm Ngọc Mai`.
- KYC: approved, có profile đầy đủ ngày sinh, giới tính, địa chỉ, CCCD masked.
- Trạng thái trước demo: chưa có khu trọ public.
- Sau khi bấm đăng ký chủ trọ: account được cấp vai landlord.
- Hồ sơ khu trọ pending: `Khu trọ An Nhiên`.
- Địa chỉ: khu vực đại học, ưu tiên Đà Nẵng/Cần Thơ/Hà Nội tùy data administrative hiện có.
- Ảnh khu trọ: 3-5 ảnh thật trên S3.
- Phòng seed: 5 phòng `A01`, `A02`, `A03`, `B01`, `B02`.
- Ảnh phòng: mỗi phòng 3-5 ảnh thật trên S3.
- Giá phòng: 2.800.000-4.200.000 đ/tháng.
- Luật khu trọ: có bản ghi `rooming_house_rules` đầy đủ để admin xem khi duyệt.
- Trạng thái ban đầu của khu trọ: `Pending/Hidden`; sau admin duyệt thì `Approved/Visible`.

### Gói C - Dữ Liệu Liên Kết Cho Demo Không Bị Đứt Mạch

- Lịch xem phòng giữa `nguyenxuanhuan.dev@gmail.com` và chủ trọ `nguyenxuanhuan21102005@gmail.com`.
- Chat direct giữa tenant và chủ trọ ở khu `Khu trọ Xuân Huân`.
- Review sau khi hợp đồng luồng 2 kết thúc, có 1 review tốt AI approved và 1 review rủi ro chuyển admin.
- Wallet transaction ghi rõ dòng tiền: tenant trả hóa đơn, landlord nhận tiền, cọc bị forfeited khi hủy trước hạn.
- Không seed thêm public name có chữ `demo`, `mock`, `test`, hoặc mã phòng kiểu `#123`.
