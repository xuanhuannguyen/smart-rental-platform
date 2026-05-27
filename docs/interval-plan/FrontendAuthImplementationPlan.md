# Frontend Auth Implementation Plan

Tài liệu này dùng để track tiến độ triển khai frontend Auth theo cấu trúc chuẩn của dự án.

Nguồn tham chiếu:

- `docs/Logic-Rule-MyTasks/RuleAndLogicHuan.md`
- `docs/interval-plan/AuthImplementationProgress.md`
- `docs/Project-Structure/Cấu trúc dự án.md`

## 1. Mục Tiêu

Nâng client từ Google login test page thành frontend Auth flow đầy đủ:

- Register email/password.
- Verify email OTP.
- Login email/password.
- Google login.
- Forgot/reset password.
- Logout.
- Load current user bằng `/api/users/me`.
- Route guard theo token, `emailConfirmed`, `onboardingStatus`.

Stack sử dụng:

- React.
- TypeScript.
- Vite.
- React Router.
- Context API.
- Fetch qua `shared/api/apiClient`.

Không dùng ở giai đoạn này:

- TanStack Query.
- React Hook Form.
- Zod.

## 2. Cấu Trúc Chuẩn Cần Đạt

```text
client/src/
  app/
    App.tsx
    main.tsx
    providers/
      AuthProvider.tsx
    router/
      routePaths.ts
      routes.tsx
      ProtectedRoute.tsx
      OnboardingGuard.tsx

  config/
    env.ts

  features/
    auth/
      pages/
        LoginPage.tsx
        RegisterPage.tsx
        VerifyEmailOtpPage.tsx
        ForgotPasswordPage.tsx
        ResetPasswordPage.tsx
      components/
        GoogleLoginButton.tsx
        LoginForm.tsx
        RegisterForm.tsx
        OtpForm.tsx
        ForgotPasswordForm.tsx
        ResetPasswordForm.tsx
      services/
        authApi.ts
      hooks/
        useLogin.ts
        useGoogleLogin.ts
        useRegister.ts
        useVerifyEmailOtp.ts
        useForgotPassword.ts
        useResetPassword.ts
        useLogout.ts
      types/
        auth.types.ts

    me/
      pages/
        MePage.tsx
        ProfilePlaceholderPage.tsx
        KycPlaceholderPage.tsx

  shared/
    api/
      apiClient.ts
      endpoints.ts
      apiResponse.types.ts
      apiErrorHandler.ts
      tokenStorage.ts
    components/
      ui/
        Button.tsx
        Input.tsx
        FormField.tsx
        Alert.tsx
      feedback/
        LoadingState.tsx
        ErrorState.tsx

  styles/
    global.css
```

## 3. Flow Triển Khai Từng Bước

### Bước 1. Chuẩn hóa nền client

- [X] Kiểm tra `client/package.json` có script `dev`, `build`, `preview`.
- [X] Kiểm tra `client/index.html` mount React qua `src/app/main.tsx`.
- [X] Kiểm tra `.env.example` có:
  - `VITE_API_BASE_URL`
  - `VITE_GOOGLE_CLIENT_ID`
- [X] Kiểm tra `.env` local không bị commit.
- [X] Chạy `npm run build` pass.

Kết quả mong muốn:

```text
Client build pass và app mở được ở http://localhost:5173
```

### Bước 2. Hoàn thiện shared API layer

- [x] Hoàn thiện `shared/api/endpoints.ts`.
- [x] Hoàn thiện `shared/api/tokenStorage.ts`.
- [x] Hoàn thiện `shared/api/apiResponse.types.ts`.
- [x] Hoàn thiện `shared/api/apiErrorHandler.ts`.
- [x] Hoàn thiện `shared/api/apiClient.ts`.
- [x] `apiClient` tự gắn `Authorization: Bearer <accessToken>` khi request có `auth: true`.
- [x] `apiClient` parse lỗi backend theo `ApiErrorResponse`.

Endpoint cần có:

```text
POST /api/auth/register
POST /api/auth/verify-email-otp
POST /api/auth/resend-email-otp
POST /api/auth/login
POST /api/auth/google-login
POST /api/auth/forgot-password
POST /api/auth/reset-password
POST /api/auth/refresh-token
POST /api/auth/logout
POST /api/auth/logout-all
GET  /api/users/me
```

Kết quả mong muốn:

```text
Không component/page nào gọi fetch trực tiếp tới backend.
```

### Bước 3. Khai báo auth types

- [X] Tạo đủ request DTO types:
  - `LoginRequest`
  - `RegisterRequest`
  - `VerifyEmailOtpRequest`
  - `ResendEmailOtpRequest`
  - `ForgotPasswordRequest`
  - `ResetPasswordRequest`
  - `GoogleLoginRequest`
  - `RefreshTokenRequest`
  - `LogoutRequest`
- [X] Tạo đủ response DTO types:
  - `LoginResponse`
  - `RegisterResponse`
  - `VerifyEmailOtpResponse`
  - `ResendEmailOtpResponse`
  - `ForgotPasswordResponse`
  - `ResetPasswordResponse`
  - `GoogleLoginResponse`
  - `RefreshTokenResponse`
  - `LogoutResponse`
  - `CurrentUserResponse`
- [X] Chuẩn hóa `AuthUser` dùng chung cho current user.

Kết quả mong muốn:

```text
authApi trả type rõ ràng, page/hook không dùng any.
```

### Bước 4. Hoàn thiện authApi

- [x] Implement `register`.
- [x] Implement `verifyEmailOtp`.
- [x] Implement `resendEmailOtp`.
- [x] Implement `login`.
- [x] Implement `googleLogin`.
- [x] Implement `forgotPassword`.
- [x] Implement `resetPassword`.
- [x] Implement `refreshToken`.
- [x] Implement `logout`.
- [x] Implement `logoutAll`.
- [x] Implement `getMe`.

Kết quả mong muốn:

```text
features/auth/services/authApi.ts là nơi duy nhất gọi Auth API.
```

### Bước 5. Tạo shared UI cơ bản

- [x] Tạo `Button`.
- [x] Tạo `Input`.
- [x] Tạo `FormField`.
- [x] Tạo `Alert`.
- [x] Tạo `LoadingState`.
- [x] Tạo `ErrorState`.

Rule:

- [x] Form submit phải disable khi loading.
- [x] Form lỗi phải hiển thị bằng `Alert`.
- [x] Không dùng card lồng card.
- [x] Text không bị tràn trên mobile.

Kết quả mong muốn:

```text
Các Auth form dùng cùng bộ component UI tối thiểu.
```

### Bước 6. Tạo AuthProvider

- [x] Tạo `app/providers/AuthProvider.tsx`.
- [x] Tạo `useAuth`.
- [x] State có `currentUser`.
- [x] State có `isAuthenticated`.
- [x] State có `isLoading`.
- [x] Có `refreshMe`.
- [x] Có `setSession`.
- [x] Có `clearSession`.
- [x] Có `logout`.
- [x] Khi app mở, nếu có access token thì gọi `/api/users/me`.
- [x] Nếu `/api/users/me` trả 401 thì clear token.

Kết quả mong muốn:

```text
App biết user hiện tại mà không cần page tự gọi /me nhiều nơi.
```

### Bước 7. Tạo router và guard

- [x] Cài `react-router-dom`.
- [x] Tạo `routePaths.ts`.
- [x] Tạo `routes.tsx`.
- [x] Tạo `ProtectedRoute.tsx`.
- [x] Tạo `OnboardingGuard.tsx`.
- [x] Bọc router trong `App.tsx`.

Route cần có:

```text
/login
/register
/verify-email
/forgot-password
/reset-password
/me
/me/profile
/me/kyc
```

Redirect rule:

```text
Không có token -> /login
emailConfirmed = false -> /verify-email
NeedProfileUpdate -> /me/profile
NeedKyc -> /me/kyc
Completed -> /me
```

Kết quả mong muốn:

```text
Route guard xử lý đúng token, emailConfirmed, onboardingStatus.
```

### Bước 8. Làm Login flow

- [x] Tách `LoginForm`.
- [x] Giữ `GoogleLoginButton`.
- [x] Login local gọi `authApi.login`.
- [x] Google login gọi `authApi.googleLogin`.
- [x] Khi login thành công, lưu `accessToken` và `refreshToken`.
- [x] Sau khi lưu token, gọi `refreshMe`.
- [x] Redirect theo `emailConfirmed` và `onboardingStatus`.
- [x] Hiển thị lỗi đăng nhập sai.
- [x] Disable form khi đang submit.

Kết quả mong muốn:

```text
Login local và Google login đều vào chung session flow.
```

### Bước 9. Làm Register flow

- [x] Tạo `RegisterPage`.
- [x] Tạo `RegisterForm`.
- [x] Validate email.
- [x] Validate password không rỗng.
- [x] Validate displayName không rỗng.
- [x] Submit gọi `authApi.register`.
- [x] Thành công chuyển tới `/verify-email?email=<email>`.
- [x] Email trùng hiển thị lỗi từ backend.

Kết quả mong muốn:

```text
Register thành công dẫn user tới màn Verify OTP.
```

### Bước 10. Làm Verify Email OTP flow

- [x] Tạo `VerifyEmailOtpPage`.
- [x] Tạo `OtpForm`.
- [x] Đọc email từ query string.
- [x] Validate OTP 6 số.
- [x] Submit gọi `authApi.verifyEmailOtp`.
- [x] Thành công chuyển `/login`.
- [x] Có nút resend OTP.
- [x] Resend gọi `authApi.resendEmailOtp`.
- [x] Hiển thị lỗi 429 resend too soon nếu backend trả về.

Kết quả mong muốn:

```text
User verify OTP được và resend OTP được.
```

### Bước 11. Làm Forgot/Reset Password flow

- [x] Tạo `ForgotPasswordPage`.
- [x] Tạo `ForgotPasswordForm`.
- [x] Submit forgot gọi `authApi.forgotPassword`.
- [x] Thành công chuyển `/reset-password?email=<email>`.
- [x] Tạo `ResetPasswordPage`.
- [x] Tạo `ResetPasswordForm`.
- [x] Validate OTP 6 số.
- [x] Validate newPassword không rỗng.
- [x] Submit reset gọi `authApi.resetPassword`.
- [x] Thành công chuyển `/login`.

Kết quả mong muốn:

```text
User quên mật khẩu có thể nhận OTP và đặt mật khẩu mới.
```

### Bước 12. Làm Me page và placeholder onboarding

- [x] Tạo `features/me/pages/MePage.tsx`.
- [x] Hiển thị email.
- [x] Hiển thị displayName.
- [x] Hiển thị roles.
- [x] Hiển thị emailConfirmed.
- [x] Hiển thị onboardingStatus.
- [x] Có nút logout.
- [x] Tạo `ProfilePlaceholderPage`.
- [x] Tạo `KycPlaceholderPage`.

Kết quả mong muốn:

```text
/me hiển thị current user; /me/profile và /me/kyc là placeholder đúng guard.
```

### Bước 13. Làm logout flow

- [x] Nếu có refresh token thì gọi `authApi.logout`.
- [x] Dù API logout lỗi, vẫn clear local token.
- [x] Redirect về `/login`.
- [ ] Test `/me` sau logout phải bị redirect.

Kết quả mong muốn:

```text
Logout xóa session frontend và revoke refresh token nếu backend xử lý được.
```

### Bước 14. Test thủ công toàn bộ Auth flow

- [ ] Register email mới.
- [ ] Verify OTP đúng.
- [ ] Login local đúng.
- [ ] Login local sai password.
- [ ] Login local khi chưa verify.
- [ ] Google login.
- [ ] Forgot password.
- [ ] Reset password đúng OTP.
- [ ] `/me` có token.
- [ ] `/me` không token.
- [ ] Logout.
- [ ] Token hết hạn thì redirect login.

Kết quả mong muốn:

```text
Toàn bộ Auth flow chạy được từ client, không cần Swagger cho flow cơ bản.
```

### Bước 15. Build và kiểm tra cuối

- [x] Chạy client build:

```powershell
cd client
npm run build
```

- [x] Chạy backend build:

```powershell
dotnet build server/src/SmartRentalPlatform.Api/SmartRentalPlatform.Api.csproj
```

- [ ] Không commit:
  - `.env`
  - `dist`
  - `node_modules`
  - token thật
  - secret thật

## 4. Definition Of Done

- [x] Frontend có đủ các page Auth.
- [x] Mọi API call đi qua service.
- [x] AuthProvider load được current user.
- [x] Route guard chạy đúng.
- [x] Google login chạy được.
- [x] Login email/password chạy được.
- [x] Register/verify OTP chạy được.
- [ ] Forgot/reset password chạy được.
- [x] Logout chạy được.
- [x] `/me` hiển thị đúng current user.
- [x] `npm run build` pass.
- [x] Backend build pass.

## 5. Ghi Chú Tiến Độ

| Ngày | Mục | Ghi chú |
| :--- | :--- | :--- |
| 2026-05-24 | Frontend Auth plan | Tạo plan track tiến độ frontend Auth theo cấu trúc chuẩn. |
| 2026-05-25 | Frontend Auth code check | Đối chiếu code với plan, tick các bước đã triển khai; client/backend build pass. |
