**RULE NGƯỜI 1**

**AUTH / ACCOUNT FLOW \- INTERVAL 1**

*Nền tảng quản lý thuê trọ / tìm trọ*

Công nghệ: ASP.NET Core Web API \+ React \+ PostgreSQL \+ GitHub

| Mục tiêu tài liệu Chuẩn hóa toàn bộ rule cho Người 1 phụ trách Auth Flow: đăng ký email/password, OTP email 15 phút, login, Google login tự động link, forgot/reset password, refresh token rotation, logout, /users/me, test case và Definition of Done. |
| :---- |

 

| Thông tin | Nội dung |
| :---- | :---- |
| Người phụ trách | Người 1 |
| Module | Account / Authentication |
| Branch đề xuất | feature/i1-auth-flow |
| Nguyên tắc chính | Một email chỉ có một USERS; EmailConfirmed chặn trước Home; Google tự link ngầm; refresh token rotation. |

 

# **Mục lục**

·         1\. Phạm vi và mục tiêu Người 1  
·         2\. Bảng Người 1 làm chủ  
·         3\. Quyết định nghiệp vụ cốt lõi  
·         4\. Danh sách API cần triển khai  
·         5\. Rule đăng ký email/password  
·         6\. Rule xác thực OTP email  
·         7\. Rule đăng nhập email/password  
·         8\. Rule Google login tự động link  
·         9\. Rule quên mật khẩu và reset password  
·         10\. Rule refresh token rotation  
·         11\. Rule logout và logout all  
·         12\. Rule /api/users/me  
·         13\. Frontend cần làm  
·         14\. Test case bắt buộc  
·         15\. Definition of Done  
·         16\. PR template

# **1\. Phạm vi và mục tiêu Người 1**

Người 1 phụ trách toàn bộ Auth Flow trong Interval 1\. Đây là phần nền móng để các thành viên khác có thể dùng user hiện tại, token, trạng thái xác thực email, trạng thái onboarding và role khi triển khai các module Role/Profile, KYC, Property/Room và Admin/Public Listing.

| Nhóm chức năng | Trong phạm vi Người 1 | Không làm ở Interval 1 |
| :---- | :---- | :---- |
| Register | Đăng ký email/password, gửi OTP xác thực email 15 phút. | Không làm đăng ký bằng số điện thoại. |
| Email Verification | Verify OTP, resend OTP, chặn user chưa xác thực trước Home. | Không thêm OnboardingStatus \= NeedVerification. |
| Login | Login email/password, lockout, login logs. | Không làm 2FA nâng cao. |
| Google Login | Google login, tự động link email trùng qua external\_logins. | Không làm LinkedAccountPage, Link/Unlink Google UI. |
| Forgot Password | Gửi OTP reset password, reset password, revoke refresh token cũ. | Không làm reset qua SMS. |
| Token | Access token, refresh token rotation, logout, logout all. | Không cần multi-device management UI. |

| Câu chốt Người 1 không chỉ làm màn hình đăng nhập. Người 1 xây nền bảo mật tài khoản, token và nhận diện user cho toàn bộ dự án. |
| :---- |

 

# **2\. Bảng Người 1 làm chủ**

| Bảng | Mục đích | Owner | Người phụ thuộc |
| :---- | :---- | :---- | :---- |
| users | Lưu email, password hash, status, EmailConfirmed, OnboardingStatus, login metadata. | Người 1 | Người 2, 3, 4, 5 |
| user\_tokens | Lưu VerifyEmail OTP, ResetPassword OTP, Refresh Token đã hash. | Người 1 | Người 2, 3, 4, 5 |
| login\_logs | Ghi lịch sử đăng nhập thành công/thất bại. | Người 1 | Admin sau này có thể xem audit. |
| external\_logins | Lưu liên kết Google login với user hiện tại. | Người 1 | Frontend Login, Admin audit sau này. |

Rule owner dữ liệu:  
 \- Người khác được đọc users qua API/service đã thống nhất.  
 \- Người khác không tự sửa field, enum, migration của nhóm bảng Auth.  
 \- Nếu cần thay đổi bảng/API Auth, phải tạo issue \[DATA CHANGE\] hoặc \[API CHANGE\].

# **3\. Quyết định nghiệp vụ cốt lõi**

| Quyết định | Rule chốt |
| :---- | :---- |
| Một email một tài khoản | Một email chỉ có một dòng USERS. Email/password và Google chỉ là hai phương thức đăng nhập của cùng một tài khoản. |
| Không thêm NeedVerification | Không tạo OnboardingStatus \= NeedVerification vì xác thực email đã có EmailConfirmed. |
| OTP trước Home | Nếu EmailConfirmed \= false thì không cho vào Home, Dashboard hoặc onboarding; phải chuyển VerifyEmailOtpPage. |
| Google tự link | Nếu Google email trùng user đã có, backend tự tạo external\_logins, không cần màn hình link Google. |
| Google không vượt OTP | Nếu tài khoản local chưa verify OTP, login Google cùng email vẫn phải chuyển VerifyEmailOtpPage. |
| Google trước, password sau | Không cho register email/password mới; chỉ cho thiết lập password qua Forgot/Reset Password. |
| Token rotation | Mỗi refresh token chỉ dùng một lần; token cũ bị rotated, token mới được cấp cùng TokenFamilyId. |

Thứ tự redirect chuẩn:  
 EmailConfirmed \= false \-\> VerifyEmailOtpPage  
 EmailConfirmed \= true \+ NeedRoleSelection \-\> RoleSelectionPage  
 EmailConfirmed \= true \+ NeedProfileUpdate \-\> ProfileUpdatePage  
 EmailConfirmed \= true \+ NeedKyc \-\> KycSubmitPage  
 EmailConfirmed \= true \+ Completed \-\> Home/Dashboard

# **4\. Danh sách API cần triển khai**

| Nhóm | Method | Endpoint | Mục đích |
| :---- | :---- | :---- | :---- |
| Register | POST | /api/auth/register | Đăng ký email/password và gửi OTP verify email. |
| Register | POST | /api/auth/verify-email-otp | Xác thực OTP email. |
| Register | POST | /api/auth/resend-email-otp | Gửi lại OTP email. |
| Login | POST | /api/auth/login | Đăng nhập email/password. |
| Login | POST | /api/auth/google-login | Đăng nhập Google và tự link nếu email trùng. |
| Password | POST | /api/auth/forgot-password | Gửi OTP reset password. |
| Password | POST | /api/auth/reset-password | Đặt lại mật khẩu bằng OTP. |
| Token | POST | /api/auth/refresh-token | Refresh token rotation. |
| Token | POST | /api/auth/logout | Logout một thiết bị, revoke refresh token hiện tại. |
| Token | POST | /api/auth/logout-all | Logout tất cả thiết bị, revoke toàn bộ refresh token. |
| User | GET | /api/users/me | Lấy user hiện tại, emailConfirmed, onboardingStatus, roles. |

# **5\. Rule đăng ký email/password**

POST /api/auth/register  
 Request:  
 {  
   "email": "user@gmail.com",  
   "password": "123456",  
   "displayName": "Nguyen Van A",  
   "phoneNumber": "0900000000"  
 }

·         Nhận email, password, displayName, phoneNumber.  
·         Normalize email, ví dụ user@gmail.com \-\> USER@GMAIL.COM.  
·         Kiểm tra NormalizedEmail đã tồn tại chưa.  
·         Nếu email chưa tồn tại: hash password và tạo users.  
·         Set Status \= Active, EmailConfirmed \= false, PhoneConfirmed \= false, OnboardingStatus \= NeedRoleSelection.  
·         Tạo OTP xác thực email, hash OTP rồi lưu vào user\_tokens.TokenHash.  
·         Set TokenType \= VerifyEmail, ExpiresAt \= now \+ 15 phút.  
·         Gửi OTP về email và trả response yêu cầu xác thực OTP.

Response thành công:  
 {  
   "success": true,  
   "userId": "uuid",  
   "email": "user@gmail.com",  
   "emailConfirmed": false,  
   "onboardingStatus": "NeedRoleSelection",  
   "message": "OTP xác thực email đã được gửi. OTP hết hạn sau 15 phút."  
 }

## **5.1. Register khi email đã tồn tại**

| Tình huống | Backend xử lý | Response code đề xuất |
| :---- | :---- | :---- |
| Email đã có do đăng ký local | Không tạo user mới. Yêu cầu login hoặc forgot password. | EMAIL\_ALREADY\_EXISTS |
| Email đã có do Google login trước | Không tạo user mới. Yêu cầu login Google hoặc set password qua forgot/reset password. | GOOGLE\_ACCOUNT\_EXISTS |

# **6\. Rule xác thực OTP email**

POST /api/auth/verify-email-otp  
 Request:  
 {  
   "email": "user@gmail.com",  
   "otp": "123456"  
 }

·         Normalize email và tìm user theo NormalizedEmail.  
·         Tìm VerifyEmail token mới nhất còn hiệu lực: ExpiresAt \> now, UsedAt \= null, RevokedAt \= null.  
·         Hash OTP user nhập và so sánh với user\_tokens.TokenHash.  
·         Nếu đúng: set users.EmailConfirmed \= true và user\_tokens.UsedAt \= now.  
·         Nếu sai hoặc hết hạn: không đổi EmailConfirmed và trả lỗi.

Response thành công:  
 {  
   "success": true,  
   "email": "user@gmail.com",  
   "emailConfirmed": true,  
   "message": "Xác thực email thành công. Bạn có thể đăng nhập."  
 }

## **6.1. Rule gửi lại OTP email**

POST /api/auth/resend-email-otp  
 Request:  
 {  
   "email": "user@gmail.com"  
 }

·         Nếu user không tồn tại: trả message chung để tránh lộ email có tồn tại hay không.  
·         Nếu EmailConfirmed \= true: trả message email đã xác thực.  
·         Nếu EmailConfirmed \= false: revoke OTP VerifyEmail cũ nếu còn hiệu lực, tạo OTP mới 15 phút và gửi email.  
·         Chống spam: giới hạn gửi lại OTP, ví dụ mỗi 60 giây mới được gửi lại.

# **7\. Rule đăng nhập email/password**

POST /api/auth/login  
 Request:  
 {  
   "email": "user@gmail.com",  
   "password": "123456"  
 }

| Bước | Điều kiện | Kết quả |
| :---- | :---- | :---- |
| 1 | User không tồn tại | Ghi login\_logs thất bại, trả lỗi chung. |
| 2 | Status \= Banned / Deleted | Từ chối đăng nhập, ghi log thất bại. |
| 3 | Đang LockoutEndAt | Từ chối đăng nhập, ghi log thất bại. |
| 4 | PasswordHash \= null | Tài khoản Google-only, yêu cầu Google login hoặc set password. |
| 5 | Password sai | Tăng AccessFailedCount; sai quá 5 lần thì lock 15 phút. |
| 6 | Password đúng nhưng EmailConfirmed \= false | Không cấp token, trả requiresEmailVerification \= true. |
| 7 | Password đúng và EmailConfirmed \= true | Cấp access token \+ refresh token rotation; ghi log thành công. |

Response nếu chưa xác thực OTP:  
 {  
   "success": false,  
   "requiresEmailVerification": true,  
   "email": "user@gmail.com",  
   "message": "Vui lòng xác thực OTP email trước khi tiếp tục."  
 }

# **8\. Rule Google login tự động link**

POST /api/auth/google-login  
 Request:  
 {  
   "idToken": "google-id-token"  
 }

·         Backend verify Google idToken.  
·         Lấy ProviderUserId, ProviderEmail, ProviderDisplayName, ProviderAvatarUrl, EmailVerified.  
·         Tìm external\_logins theo Provider \= Google và ProviderUserId.  
·         Nếu external\_login đã có: login vào user tương ứng nếu user không bị khóa/cấm/xóa.  
·         Nếu chưa có external\_login: tìm users theo NormalizedEmail của ProviderEmail.

| Tình huống | Backend xử lý | Kết quả |
| :---- | :---- | :---- |
| Google email chưa tồn tại | Tạo user mới, PasswordHash \= null, EmailConfirmed \= true, OnboardingStatus \= NeedRoleSelection, tạo external\_logins. | Cho login và redirect theo OnboardingStatus. |
| Email/password trước, Google sau, EmailConfirmed \= true | Không tạo user mới, tạo external\_logins nếu chưa có. | Cho login và redirect theo OnboardingStatus. |
| Email/password trước, Google sau, EmailConfirmed \= false | Tự link Google vào user cũ nhưng không cho vào Home. Có thể gửi lại OTP. | Trả requiresEmailVerification \= true, frontend sang VerifyEmailOtpPage. |
| Google trước, register email/password sau | Không cho register user mới cùng email. | Yêu cầu login Google hoặc set password qua forgot/reset password. |

| Không làm màn hình liên kết Google Interval 1 không làm LinkedAccountPage, LinkGoogleButton, UnlinkGoogleButton, API link-google thủ công hoặc unlink-google. Google linking là logic backend tự xử lý theo email trùng. |
| :---- |

 

# **9\. Rule quên mật khẩu và reset password**

## **9.1. Forgot password**

POST /api/auth/forgot-password  
 Request:  
 {  
   "email": "user@gmail.com"  
 }

·         Normalize email và tìm user.  
·         Luôn trả message chung: “Nếu email tồn tại, hệ thống đã gửi OTP đặt lại mật khẩu.”  
·         Nếu user tồn tại: revoke ResetPassword token cũ nếu còn hiệu lực.  
·         Tạo OTP reset password, hash OTP, lưu TokenType \= ResetPassword, ExpiresAt \= now \+ 15 phút.  
·         Gửi OTP về email.

Response:  
 {  
   "message": "Nếu email tồn tại, hệ thống đã gửi OTP đặt lại mật khẩu."  
 }

## **9.2. Reset password**

POST /api/auth/reset-password  
 Request:  
 {  
   "email": "user@gmail.com",  
   "otp": "123456",  
   "newPassword": "NewPassword123"  
 }

·         Tìm ResetPassword token còn hiệu lực: ExpiresAt \> now, UsedAt \= null, RevokedAt \= null.  
·         Hash OTP user nhập và so sánh TokenHash.  
·         Nếu OTP hợp lệ: hash newPassword và cập nhật users.PasswordHash.  
·         Nếu EmailConfirmed \= false thì có thể set EmailConfirmed \= true vì user đã xác thực qua OTP email.  
·         Set ResetPassword token UsedAt \= now.  
·         Revoke toàn bộ refresh token còn hiệu lực của user với RevokedReason \= PasswordChanged.

| Tài khoản Google-only Nếu user tạo tài khoản bằng Google trước thì PasswordHash \= null. Khi reset password thành công, tài khoản đó có thể đăng nhập bằng cả Google và email/password. |
| :---- |

 

# **10\. Rule refresh token rotation**

| Nguyên tắc token rotation Mỗi refresh token chỉ được dùng một lần. Khi refresh thành công, token cũ bị đánh dấu UsedAt và RevokedAt với RevokedReason \= TokenRotated; backend cấp refresh token mới cùng TokenFamilyId. Nếu token cũ bị dùng lại, hệ thống coi là nghi ngờ bị lộ token và revoke toàn bộ token family. |
| :---- |

 

| Field trong user\_tokens | Mục đích |
| :---- | :---- |
| Id | Khóa chính token. |
| UserId | Token thuộc user nào. |
| TokenType | Refresh, VerifyEmail, ResetPassword. |
| TokenHash | Hash của token/OTP, không lưu plain text. |
| TokenFamilyId | Gom các refresh token trong cùng một phiên đăng nhập. |
| ReplacedByTokenId | Token mới thay thế token cũ. |
| ExpiresAt | Thời điểm hết hạn. |
| UsedAt | Token đã được dùng. |
| RevokedAt | Token đã bị thu hồi. |
| RevokedReason | TokenRotated, Logout, ReuseDetected, PasswordChanged, LogoutAllDevices. |
| CreatedByIp / UserAgent | Audit nguồn tạo token. |

## **10.1. Khi login thành công**

·         Tạo access token ngắn hạn.  
·         Tạo refresh token raw.  
·         Hash refresh token và lưu vào user\_tokens.  
·         Tạo TokenFamilyId mới cho phiên đăng nhập.  
·         Set TokenType \= Refresh, UsedAt \= null, RevokedAt \= null.  
·         Trả accessToken \+ refreshToken cho frontend.

## **10.2. Khi refresh token**

POST /api/auth/refresh-token  
 Request:  
 {  
   "refreshToken": "refresh-token-raw"  
 }

·         Hash refresh token gửi lên và tìm trong user\_tokens.  
·         Nếu không tìm thấy, hết hạn hoặc RevokedAt \!= null thì trả 401\.  
·         Nếu UsedAt \!= null: đây là reuse token cũ, revoke toàn bộ token cùng TokenFamilyId với RevokedReason \= ReuseDetected và bắt user đăng nhập lại.  
·         Nếu token hợp lệ: set token cũ UsedAt \= now, RevokedAt \= now, RevokedReason \= TokenRotated.  
·         Tạo refresh token mới, hash và lưu token mới cùng TokenFamilyId.  
·         Set token cũ ReplacedByTokenId \= token mới.  
·         Cấp access token mới và refresh token mới.

# **11\. Rule logout và logout all**

| API | Backend xử lý | Kết quả |
| :---- | :---- | :---- |
| POST /api/auth/logout | Hash refresh token hiện tại, tìm user\_tokens, set RevokedAt \= now, RevokedReason \= Logout. | Logout một thiết bị. |
| POST /api/auth/logout-all | Lấy userId từ access token, revoke toàn bộ refresh token còn hiệu lực, RevokedReason \= LogoutAllDevices. | Logout tất cả thiết bị. |

# **12\. Rule /api/users/me**

GET /api/users/me  
 Response:  
 {  
   "userId": "uuid",  
   "email": "user@gmail.com",  
   "displayName": "Nguyen Van A",  
   "avatarUrl": null,  
   "emailConfirmed": true,  
   "status": "Active",  
   "onboardingStatus": "NeedRoleSelection",  
   "roles": \["Tenant"\]  
 }

| Rule phụ thuộc Không đổi response /api/users/me tùy tiện vì Người 2, 3, 4, 5 đều phụ thuộc API này để redirect, check role, check onboarding và phân quyền. |
| :---- |

 

# **13\. Frontend Người 1 phải làm**

| Màn hình / Component | Mục đích |
| :---- | :---- |
| LoginPage | Đăng nhập email/password và nút Google login. |
| RegisterPage | Đăng ký email/password. |
| VerifyEmailOtpPage | Nhập OTP email và resend OTP. |
| ForgotPasswordPage | Nhập email để nhận OTP reset password. |
| ResetPasswordPage | Nhập OTP và mật khẩu mới. |
| GoogleLoginButton | Lấy Google idToken gửi về backend. |
| AuthStore | Lưu accessToken, refreshToken, currentUser, isAuthenticated. |
| AuthGuard | Chặn route cần login; ưu tiên check EmailConfirmed trước OnboardingStatus. |
| apiClient | Gắn access token vào request; gọi refresh token rotation khi cần. |

Frontend redirect rule:  
 1\. Sau register \-\> VerifyEmailOtpPage.  
 2\. Sau login \-\> gọi /api/users/me.  
 3\. Nếu emailConfirmed \= false \-\> VerifyEmailOtpPage.  
 4\. Nếu emailConfirmed \= true \-\> redirect theo onboardingStatus.  
 5\. Không có LinkedAccountPage, không có LinkGoogleButton/UnlinkGoogleButton trong Profile.

# **14\. Test case bắt buộc**

| Mã test | Nội dung kiểm thử | Kết quả mong đợi |
| :---- | :---- | :---- |
| TC-AUTH-01 | Register email mới. | Tạo user EmailConfirmed=false, OnboardingStatus=NeedRoleSelection. |
| TC-AUTH-02 | Register email mới. | Tạo VerifyEmail OTP hết hạn sau 15 phút. |
| TC-AUTH-03 | Register email trùng local. | Bị chặn EMAIL\_ALREADY\_EXISTS. |
| TC-AUTH-04 | Register email trùng với Google-created account. | Bị chặn GOOGLE\_ACCOUNT\_EXISTS. |
| TC-AUTH-05 | Verify email OTP đúng. | EmailConfirmed=true. |
| TC-AUTH-06 | Verify email OTP sai. | Bị từ chối. |
| TC-AUTH-07 | Verify email OTP hết hạn. | Bị từ chối. |
| TC-AUTH-08 | Resend OTP. | Revoke OTP cũ, tạo OTP mới 15 phút. |
| TC-AUTH-09 | Login local khi EmailConfirmed=false. | Không cấp token, trả requiresEmailVerification=true. |
| TC-AUTH-10 | Login local khi EmailConfirmed=true. | Cấp access token \+ refresh token. |
| TC-AUTH-11 | Login sai password. | Tăng AccessFailedCount và ghi login\_logs. |
| TC-AUTH-12 | Sai password quá 5 lần. | Set LockoutEndAt. |
| TC-AUTH-13 | Google email mới. | Tạo user PasswordHash=null, EmailConfirmed=true. |
| TC-AUTH-14 | Email/password trước, Google sau, EmailConfirmed=true. | Tự link và cho login. |
| TC-AUTH-15 | Email/password trước, Google sau, EmailConfirmed=false. | Tự link nhưng chuyển VerifyEmailOtpPage. |
| TC-AUTH-16 | Google trước, register email/password sau. | Không tạo user mới. |
| TC-AUTH-17 | Google-only user dùng forgot/reset password. | Cập nhật PasswordHash thành công. |
| TC-AUTH-18 | Forgot password email không tồn tại. | Trả message chung. |
| TC-AUTH-19 | Reset password OTP đúng. | Đổi PasswordHash, revoke refresh token cũ. |
| TC-AUTH-20 | Reset password OTP hết hạn. | Không đổi password. |
| TC-AUTH-21 | Refresh token hợp lệ. | Token cũ TokenRotated, cấp token mới. |
| TC-AUTH-22 | Dùng lại refresh token cũ. | Revoke TokenFamilyId, bắt login lại. |
| TC-AUTH-23 | Logout. | Revoke refresh token hiện tại. |
| TC-AUTH-24 | Logout all. | Revoke toàn bộ refresh token của user. |
| TC-AUTH-25 | /api/users/me không token. | Trả 401\. |
| TC-AUTH-26 | /api/users/me có token. | Trả đúng user, emailConfirmed, onboardingStatus, roles. |

# **15\. Definition of Done**

·         Register email/password chạy được.  
·         Register gửi OTP xác thực email 15 phút.  
·         Verify OTP email chạy được.  
·         Resend OTP chạy được.  
·         Login email/password chạy được.  
·         Login chặn user chưa xác thực email.  
·         Google login chạy được.  
·         Google login tự link nếu email đã tồn tại.  
·         Google login không vượt qua EmailConfirmed=false.  
·         Google-created account không được register email/password mới cùng email.  
·         Forgot password gửi OTP reset password.  
·         Reset password đổi PasswordHash.  
·         Reset password revoke toàn bộ refresh token cũ.  
·         Refresh token rotation chạy đúng.  
·         Reuse refresh token cũ bị phát hiện và revoke cả token family.  
·         Logout revoke refresh token hiện tại.  
·         Logout all revoke toàn bộ refresh token user.  
·         /api/users/me trả đúng user hiện tại.  
·         Login thành công/thất bại đều ghi login\_logs.  
·         Frontend có Login/Register/VerifyEmail/ForgotPassword/ResetPassword.  
·         Frontend redirect đúng theo EmailConfirmed trước, OnboardingStatus sau.  
·         Không làm màn hình quản lý liên kết Google.  
·         Không lưu OTP/token plain text trong database.  
·         Không commit .env, token thật, Google secret thật lên GitHub.

# **16\. PR template đề xuất**

Title: \[I1\]\[P1\] Auth Flow \- Email OTP, Google Login, Token Rotation

 Owner: Người 1

 Backend:  
 \- POST /api/auth/register  
 \- POST /api/auth/verify-email-otp  
 \- POST /api/auth/resend-email-otp  
 \- POST /api/auth/login  
 \- POST /api/auth/google-login  
 \- POST /api/auth/forgot-password  
 \- POST /api/auth/reset-password  
 \- POST /api/auth/refresh-token  
 \- POST /api/auth/logout  
 \- POST /api/auth/logout-all  
 \- GET /api/users/me

 Frontend:  
 \- LoginPage  
 \- RegisterPage  
 \- VerifyEmailOtpPage  
 \- ForgotPasswordPage  
 \- ResetPasswordPage  
 \- GoogleLoginButton  
 \- AuthStore  
 \- AuthGuard  
 \- apiClient attach token

 Database changes:  
 \- users  
 \- user\_tokens  
 \- login\_logs  
 \- external\_logins

 Business rules:  
 \- Một email chỉ có một USERS  
 \- EmailConfirmed=false chặn trước Home  
 \- OTP email hết hạn sau 15 phút  
 \- Google login tự link ngầm theo email trùng  
 \- Không làm màn hình quản lý liên kết Google  
 \- Refresh token rotation, phát hiện reuse token

 Tested:  
 \- Register / verify OTP / resend OTP  
 \- Login local / Google login  
 \- Forgot / reset password  
 \- Refresh token rotation  
 \- Logout / logout all  
 \- /users/me

 Risk:  
 \- Ảnh hưởng Người 2, 3, 4, 5 vì toàn bộ module sau dùng /api/users/me, token và EmailConfirmed.

 

# **17\. Câu chốt đưa vào tài liệu nhóm**

Người 1 phụ trách Auth Flow gồm đăng ký email/password, xác thực email bằng OTP 15 phút, đăng nhập local, Google login, quên mật khẩu, reset password, refresh token rotation, logout và /users/me. Hệ thống áp dụng nguyên tắc một email chỉ có một tài khoản USERS. Tài khoản đăng ký bằng email/password phải có EmailConfirmed=false và OnboardingStatus=NeedRoleSelection, sau đó bắt buộc xác thực OTP email trước khi được vào Home hoặc tiếp tục onboarding. Không tạo OnboardingStatus=NeedVerification vì xác thực email đã được quản lý bằng EmailConfirmed. Google login không có màn hình liên kết riêng; nếu Google email trùng với tài khoản đã tồn tại thì backend tự động tạo external\_logins để liên kết vào user cũ, không tạo user mới. Tuy nhiên, Google login không được dùng để né OTP: nếu EmailConfirmed=false thì vẫn chuyển user sang VerifyEmailOtpPage. Nếu user tạo tài khoản bằng Google trước thì không được register email/password mới cùng email; nếu muốn dùng password thì đi qua luồng Forgot Password / Reset Password để cập nhật PasswordHash. Refresh token phải dùng token rotation: mỗi refresh token chỉ dùng một lần, token cũ bị đánh dấu TokenRotated, token mới được cấp cùng TokenFamilyId; nếu phát hiện dùng lại token cũ thì revoke toàn bộ token family và bắt user đăng nhập lại.

