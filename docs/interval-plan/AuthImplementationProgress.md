# Auth Implementation Progress

Tài liệu này dùng để theo dõi tiến độ code module **Auth / Account Flow - Interval 1**. Mỗi mục chỉ được tick `[x]` khi đã code xong, build pass và test đúng theo Definition of Done của mục đó.

Nguồn nghiệp vụ chính:

- `docs/interval-plan/RuleHuan.md`
- `docs/erd/ERD_Tong_Quat_He_Thong_Thue_Tro_Hoan_Thien_v3_1_Ghi_Chu_Index.mmd`
- `docs/project-structure/Cấu trúc dự án.md`

## 0. Nền Tảng Domain Và Database

- [x] Chốt enum `OnboardingStatus`: `NeedProfileUpdate`, `NeedKyc`, `Completed`.
- [x] Chốt enum `UserStatus`: `Active`, `Locked`, `Banned`, `Deleted`.
- [x] Chốt enum `RoleName`: `Admin`, `Tenant`, `Landlord`.
- [x] Chốt enum `TokenType`: `Refresh`, `ResetPassword`, `VerifyEmail`.
- [x] Chốt enum `TokenRevokedReason`: có `TokenRotated`, `ReuseDetected`, `LogoutAllDevices`.
- [x] Chốt enum `LoginProvider`: `Local`, `Google`.
- [x] Tạo Auth entities: `User`, `Role`, `UserRole`, `UserToken`, `LoginLog`, `ExternalLogin`.
- [x] Bổ sung `TokenFamilyId`, `ReplacedByTokenId` cho refresh token rotation.
- [x] Map EF Core configurations cho Auth entities.
- [x] Cập nhật ERD bỏ `NeedRoleSelection`, thêm `token_family_id`, `replaced_by_token_id`.
- [x] Tạo migration mới sau khi chốt entity/config.
- [x] Chạy update database local thành công.
- [x] Kiểm tra bảng Auth trong PostgreSQL đúng schema.

## 1. Chuẩn Hóa Contracts Và Application Skeleton

- [x] Xóa hoặc không dùng `Contracts/Requests` và `Contracts/Responses` nếu đang theo feature folder.
- [x] Tạo `Contracts/Common/ApiResponse.cs`.
- [x] Tạo `Contracts/Common/ApiErrorResponse.cs`.
- [x] Tạo `Contracts/Common/PagedResult.cs`.
- [x] Tạo `Contracts/Common/ErrorCodes.cs`.
- [x] Tạo `Application/Common/Interfaces/IAppDbContext.cs`.
- [x] Tạo `Application/Common/Interfaces/IPasswordService.cs`.
- [x] Tạo `Application/Common/Interfaces/ITokenService.cs`.
- [x] Tạo `Application/Common/Interfaces/IEmailSender.cs`.
- [x] Tạo `Application/Common/Interfaces/ICurrentUserService.cs`.
- [x] Đăng ký service nghiệp vụ trong `ApplicationServiceRegistration.cs`.
- [x] Đăng ký service kỹ thuật trong `InfrastructureServiceRegistration.cs`.
- [x] Build solution pass.

## 2. Register Email/Password

Endpoint: `POST /api/auth/register`

- [x] Tạo `Contracts/Auth/RegisterRequest.cs`.
- [x] Tạo `Contracts/Auth/RegisterResponse.cs`.
- [x] Tạo `Application/Auth/IAuthService.cs`.
- [x] Tạo `Application/Auth/AuthService.cs`.
- [x] Implement normalize email.
- [x] Chặn email trùng bằng `NormalizedEmail`.
- [x] Hash password, không lưu plain text.
- [x] Tạo user mới với `Status = Active`.
- [x] Tạo user mới với `EmailConfirmed = false`.
- [x] Tạo user mới với `PhoneConfirmed = false`.
- [x] Tạo user mới với `OnboardingStatus = NeedProfileUpdate`.
- [x] Gán role mặc định `Tenant` trong `user_roles`.
- [x] Sinh OTP verify email.
- [x] Hash OTP trước khi lưu vào `user_tokens`.
- [x] Lưu `TokenType = VerifyEmail`, `ExpiresAt = now + 15 phút`.
- [x] Tạo `Api/Controllers/AuthController.cs`.
- [x] Expose endpoint `POST /api/auth/register`.
- [x] Build solution pass.
- [x] Test register email mới thành công.
- [x] Test register email trùng trả lỗi đúng.
- [x] Kiểm tra DB: `users`, `user_roles`, `user_tokens`.

## 3. Verify Email OTP

Endpoint: `POST /api/auth/verify-email-otp`

- [x] Tạo `Contracts/Auth/VerifyEmailOtpRequest.cs`.
- [x] Tạo response DTO nếu cần.
- [x] Tìm user bằng `NormalizedEmail`.
- [x] Tìm token `VerifyEmail` mới nhất còn hiệu lực.
- [x] So sánh OTP bằng hash.
- [x] OTP đúng: set `EmailConfirmed = true`.
- [x] OTP đúng: set token `UsedAt = now`.
- [x] OTP sai hoặc hết hạn: không đổi `EmailConfirmed`.
- [x] Expose endpoint `POST /api/auth/verify-email-otp`.
- [x] Build solution pass.
- [x] Test OTP đúng.
- [x] Test OTP sai.
- [x] Test OTP hết hạn.

## 4. Resend Email OTP

Endpoint: `POST /api/auth/resend-email-otp`

- [x] Tạo `Contracts/Auth/ResendEmailOtpRequest.cs`.
- [x] Nếu user không tồn tại, trả message chung.
- [x] Nếu `EmailConfirmed = true`, trả message email đã xác thực.
- [x] Nếu chưa xác thực, revoke OTP cũ còn hiệu lực.
- [x] Tạo OTP mới hết hạn sau 15 phút.
- [x] Thêm chống spam resend OTP nếu áp dụng trong Interval 1.
- [x] Expose endpoint `POST /api/auth/resend-email-otp`.
- [x] Build solution pass.
- [x] Test resend OTP.
- [x] Kiểm tra OTP cũ đã bị revoke.

## 5. Login Email/Password

Endpoint: `POST /api/auth/login`

- [x] Tạo `Contracts/Auth/LoginRequest.cs`.
- [x] Tạo `Contracts/Auth/LoginResponse.cs`.
- [x] Tìm user bằng `NormalizedEmail`.
- [x] Ghi `login_logs` khi user không tồn tại.
- [x] Chặn `Banned` và `Deleted`.
- [x] Chặn user đang trong thời gian `LockoutEndAt`.
- [x] Chặn Google-only account khi `PasswordHash = null`.
- [x] Verify password.
- [x] Password sai: tăng `AccessFailedCount`.
- [x] Password sai quá 5 lần: set `LockoutEndAt`.
- [x] Password đúng nhưng `EmailConfirmed = false`: không cấp token.
- [x] Password đúng và email đã xác thực: ghi login thành công.
- [x] Expose endpoint `POST /api/auth/login`.
- [x] Build solution pass.
- [x] Test login email chưa verify.
- [x] Test login sai password.
- [x] Test lockout.
- [x] Test login thành công.

## 6. JWT, Refresh Token Và /users/me

Endpoints:

- `GET /api/users/me`

- [x] Implement `ITokenService` tạo access token.
- [x] Implement refresh token raw + hash.
- [x] Lưu refresh token với `TokenFamilyId` mới khi login thành công.
- [x] Cấu hình JWT authentication trong `Api`.
- [x] Bật `app.UseAuthentication()` trước `app.UseAuthorization()`.
- [x] Tạo `Contracts/Users/CurrentUserResponse.cs`.
- [x] Tạo `Application/Users/IUserService.cs`.
- [x] Tạo `Application/Users/UserService.cs`.
- [x] Tạo `Api/Controllers/UsersController.cs`.
- [x] Expose endpoint `GET /api/users/me`.
- [x] `/users/me` trả `userId`, `email`, `displayName`, `avatarUrl`, `emailConfirmed`, `status`, `onboardingStatus`, `roles`.
- [x] Build solution pass.
- [x] Test `/users/me` không token trả 401.
- [x] Test `/users/me` có token trả đúng current user.

## 7. Refresh Token Rotation

Endpoint: `POST /api/auth/refresh-token`

- [x] Tạo `Contracts/Auth/RefreshTokenRequest.cs`.
- [x] Tạo response DTO nếu chưa có.
- [x] Hash refresh token request.
- [x] Tìm refresh token trong `user_tokens`.
- [x] Token không tồn tại/hết hạn/revoked: trả 401.
- [x] Token đã `UsedAt != null`: revoke toàn bộ `TokenFamilyId` với `ReuseDetected`.
- [x] Token hợp lệ: set token cũ `UsedAt`, `RevokedAt`, `RevokedReason = TokenRotated`.
- [x] Tạo refresh token mới cùng `TokenFamilyId`.
- [x] Set token cũ `ReplacedByTokenId = token mới`.
- [x] Trả access token mới và refresh token mới.
- [x] Expose endpoint `POST /api/auth/refresh-token`.
- [x] Build solution pass.
- [x] Test refresh token hợp lệ.
- [x] Test dùng lại refresh token cũ.

## 8. Logout Và Logout All

Endpoints:

- `POST /api/auth/logout`
- `POST /api/auth/logout-all`

- [x] Tạo `Contracts/Auth/LogoutRequest.cs` nếu logout cần refresh token body.
- [x] Logout một thiết bị: revoke refresh token hiện tại với `Logout`.
- [x] Logout all: revoke toàn bộ refresh token còn hiệu lực của user với `LogoutAllDevices`.
- [x] Expose `POST /api/auth/logout`.
- [x] Expose `POST /api/auth/logout-all`.
- [x] Build solution pass.
- [x] Test logout một thiết bị.
- [x] Test logout all.

## 9. Forgot Password Và Reset Password

Endpoints:

- `POST /api/auth/forgot-password`
- `POST /api/auth/reset-password`

- [x] Tạo `Contracts/Auth/ForgotPasswordRequest.cs`.
- [x] Tạo `Contracts/Auth/ResetPasswordRequest.cs`.
- [x] Forgot password luôn trả message chung.
- [x] Nếu user tồn tại, revoke reset token cũ còn hiệu lực.
- [x] Tạo OTP reset password, hash và lưu `TokenType = ResetPassword`.
- [x] Reset password tìm token còn hiệu lực.
- [x] OTP đúng: hash password mới và cập nhật `PasswordHash`.
- [x] Nếu `EmailConfirmed = false`, set `EmailConfirmed = true`.
- [x] Revoke toàn bộ refresh token cũ với `PasswordChanged`.
- [x] Expose forgot/reset endpoints.
- [x] Build solution pass.
- [x] Test forgot password email tồn tại.
- [x] Test forgot password email không tồn tại.
- [x] Test reset password OTP đúng.
- [x] Test reset password OTP sai/hết hạn.

## 10. Google Login

Endpoint: `POST /api/auth/google-login`

- [x] Tạo `Contracts/Auth/GoogleLoginRequest.cs`.
- [x] Tạo/hoàn thiện Google login response DTO.
- [ ] Implement service verify Google `idToken`.
- [ ] Tìm `external_logins` theo `Provider = Google` và `ProviderUserId`.
- [ ] Nếu external login đã tồn tại, login vào user tương ứng.
- [ ] Nếu Google email chưa tồn tại, tạo user mới `PasswordHash = null`, `EmailConfirmed = true`, role Tenant mặc định.
- [ ] Nếu email/password đã tồn tại và `EmailConfirmed = true`, tự tạo external login và cho login.
- [ ] Nếu email/password đã tồn tại và `EmailConfirmed = false`, tự link nhưng không cho vào Home.
- [x] Google-created account không được register email/password mới cùng email.
- [x] Expose endpoint `POST /api/auth/google-login`.
- [x] Build solution pass.
- [ ] Test Google email mới.
- [ ] Test email/password trước, Google sau.
- [ ] Test Google không vượt OTP.

## 11. Checklist Trước Khi PR

- [ ] `dotnet build server/SmartRentalPlatform.slnx` pass.
- [ ] Không có DTO request/response trong `Api`.
- [ ] Controller không query `AppDbContext` trực tiếp.
- [ ] Application không phụ thuộc trực tiếp Infrastructure.
- [ ] Không lưu OTP/token plain text trong database.
- [ ] Không hard-code secret/JWT key/Google secret.
- [ ] Error response dùng format chung.
- [ ] Test case Auth trong `RuleHuan.md` đã được tick hoặc ghi rõ chưa làm.
- [ ] Migration được review trước khi update database.
- [ ] Không commit `bin/`, `obj/`, `.env`, token thật, secret thật.

## 12. Ghi Chú Tiến Độ

Ghi lại quyết định hoặc vấn đề phát sinh trong quá trình làm:

| Ngày | Mục | Ghi chú |
| :--- | :--- | :--- |
| 2026-05-22 | Auth plan | User mới mặc định là Tenant, không dùng `NeedRoleSelection`. |
| 2026-05-22 | Register duplicate | Đã chặn tạo trùng user và trả `409 Conflict` bằng exception middleware. |
| 2026-05-22 | Verify email OTP | Đã verify OTP đúng, chặn OTP sai và đánh dấu token đã dùng bằng `UsedAt`, `RevokedAt`, `RevokedReason = Used`. |
| 2026-05-22 | Resend email OTP | Đã gửi lại OTP mới, revoke OTP cũ bằng `RevokedReason = Replaced`, sửa `token_hash` index không unique để tránh lỗi OTP trùng. |
| 2026-05-22 | Login email/password | Đã login bằng email/password, ghi `login_logs`, chặn email chưa verify, sai password và lockout. Chưa cấp JWT ở bước này. |
| 2026-05-23 | JWT và /users/me | Đã cấu hình JWT, login trả access token và refresh token, lưu refresh token dạng hash với `TokenFamilyId`, thêm `/api/users/me` và test 401/200 thành công. |
| 2026-05-23 | Refresh token rotation | Đã thêm `/api/auth/refresh-token`, rotate refresh token một lần, set `ReplacedByTokenId`, token cũ `TokenRotated`, reuse token cũ trả 401 và revoke cả family bằng `ReuseDetected`. |
| 2026-05-23 | Logout/logout-all | Đã thêm `/api/auth/logout` revoke một refresh token bằng `Logout`, `/api/auth/logout-all` yêu cầu JWT và revoke toàn bộ refresh token còn hiệu lực của user bằng `LogoutAllDevices`. |
| 2026-05-23 | Rate limit resend OTP | Đã thêm chống spam resend OTP 60 giây; test đăng ký user mới rồi resend ngay trả `429 OTP_RESEND_TOO_SOON`. |
| 2026-05-23 | Forgot/reset password | Đã thêm `/api/auth/forgot-password` và `/api/auth/reset-password`; test email tồn tại/không tồn tại, OTP đúng, OTP sai, OTP hết hạn, login bằng password mới, refresh token cũ bị revoke bằng `PasswordChanged`. |
| 2026-05-23 | Google login | Đã tạo request/response DTO, endpoint `/api/auth/google-login`, service verify Google idToken bằng `Google.Apis.Auth`, và chặn register email/password với Google-only account bằng `GOOGLE_ACCOUNT_EXISTS`. Chưa tick test Google login thành công vì chưa có Google OAuth ClientId/idToken thật để test end-to-end. |

## 13. Note Cuối Ngày 2026-05-22

### Đã hoàn thành

- Hoàn tất nền tảng Auth domain/database: `users`, `roles`, `user_roles`, `user_tokens`, `login_logs`, `external_logins`.
- Hoàn tất contracts/common response: `ApiResponse`, `ApiErrorResponse`, `PagedResult`, `ErrorCodes`.
- Hoàn tất register email/password:
  - Normalize email bằng `NormalizedEmail`.
  - Chặn email trùng và trả `409 Conflict`.
  - Hash password, không lưu plain text.
  - User mới mặc định `Tenant`, `Status = Active`, `EmailConfirmed = false`, `OnboardingStatus = NeedProfileUpdate`.
  - Sinh OTP verify email, hash OTP trước khi lưu.
- Hoàn tất verify email OTP:
  - OTP đúng thì set `EmailConfirmed = true`.
  - Token OTP được đánh dấu `UsedAt`, `RevokedAt`, `RevokedReason = Used`.
  - OTP sai/hết hạn trả lỗi nghiệp vụ, không đổi trạng thái user.
- Hoàn tất resend email OTP:
  - Email không tồn tại trả message chung.
  - Email đã xác thực không gửi OTP mới.
  - Email chưa xác thực thì revoke OTP cũ còn hiệu lực bằng `RevokedReason = Replaced`.
  - Tạo OTP mới hết hạn sau 15 phút.
- Hoàn tất login email/password giai đoạn chưa JWT:
  - Tìm user bằng `NormalizedEmail`.
  - Verify password bằng `IPasswordService`.
  - Chặn `Banned`, `Deleted`, lockout, Google-only account, email chưa verify.
  - Sai password tăng `AccessFailedCount`; sai quá 5 lần set `LockoutEndAt`.
  - Login đúng reset failed count, cập nhật `LastLoginAt`, ghi `login_logs`.
- Build cuối ngày đã pass với `dotnet build server\SmartRentalPlatform.slnx`.

### Điều cần chú ý

- `TokenHash` không được unique toàn cục. OTP chỉ có 6 số, có thể sinh trùng; vì vậy `user_tokens.token_hash` chỉ nên index thường.
- OTP/token không xóa khỏi DB sau khi dùng; chỉ đánh dấu bằng `UsedAt`, `RevokedAt`, `RevokedReason` để giữ lịch sử và chống dùng lại.
- Khi thêm `UserToken` mới cho user đã load từ DB trong resend, dùng `_dbContext.UserTokens.Add(...)`, không dùng `user.UserTokens.Add(...)` để tránh EF hiểu nhầm thành update.
- `PasswordService.VerifyPassword` phải nhận đúng thứ tự: hash trong DB trước, password user nhập sau.
- Response lỗi đã đi qua `ExceptionHandlingMiddleware`; lỗi nghiệp vụ nên ném custom exception (`UnauthorizedException`, `ForbiddenException`, `ConflictException`, `BadRequestException`) thay vì để rơi thành `500`.
- Root `http://localhost:5294/` trả `404` là bình thường; test qua `http://localhost:5294/swagger`.
- Nếu build báo DLL bị khóa bởi `SmartRentalPlatform.Api`, dừng process đang listen port `5294` rồi build lại.

### Chưa làm

- Chống spam resend OTP/rate limit chưa implement.
- Login hiện chưa cấp JWT/access token/refresh token.
- Chưa có `/api/users/me`.
- Chưa làm refresh token rotation, logout/logout-all, forgot/reset password, Google login.

### Điểm bắt đầu phiên sau

Tiếp tục từ mục **6. JWT, Refresh Token Và /users/me**:

1. Cấu hình JWT authentication trong `Api`.
2. Cho login thành công trả `accessToken` và `refreshToken`.
3. Lưu refresh token raw dạng hash vào `user_tokens` với `TokenType = Refresh`.
4. Tạo `/api/users/me` để test current user bằng JWT.

## 14. Note Cập Nhật 2026-05-23

### Đã hoàn thành thêm

- Hoàn tất JWT, refresh token khi login và `/api/users/me`.
- Hoàn tất refresh token rotation.
- Hoàn tất logout một thiết bị và logout all.
- Hoàn tất chống spam resend email OTP 60 giây.
- Hoàn tất forgot/reset password:
  - Forgot password trả message chung cho cả email tồn tại và không tồn tại.
  - Reset password đổi `PasswordHash`.
  - Reset password set `EmailConfirmed = true`.
  - Reset password revoke refresh token còn hiệu lực bằng `PasswordChanged`.
- Đã thêm phần code nền cho Google login:
  - DTO request/response.
  - Endpoint `/api/auth/google-login`.
  - `IGoogleAuthService` và `GoogleAuthService` dùng `Google.Apis.Auth`.
  - Register email/password với Google-only account trả `GOOGLE_ACCOUNT_EXISTS`.

### Chưa hoàn thành / cần bổ sung

- Google login chưa được xác nhận thành công end-to-end vì chưa có Google OAuth Client ID và `idToken` thật để test:
  - Chưa test Google email mới tạo user `PasswordHash = null`.
  - Chưa test email/password đã verify rồi Google tự link.
  - Chưa test email/password chưa verify rồi Google tự link nhưng không cấp token.
- `Authentication:Google:ClientId` trong `appsettings.Development.json` đang để trống. Cần cấu hình Client ID thật ở local secret/user-secrets/env trước khi test production-like.
- Rate limit mới áp dụng cho `resend-email-otp`. Chưa áp dụng rate limit cho:
  - `forgot-password`.
  - `login` theo IP/email ngoài lockout theo user.
  - `verify-email-otp` / `reset-password` theo số lần nhập OTP sai.
- Chưa làm frontend Auth screens trong scope RuleHuan: Login/Register/VerifyEmail/ForgotPassword/ResetPassword/GoogleLoginButton/AuthStore/AuthGuard.
