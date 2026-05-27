import { apiClient } from '../../../shared/api/apiClient';
import { ENDPOINTS } from '../../../shared/api/endpoints';
import type { ApiResponse } from '../../../shared/api/apiResponse.types';
import type {
  CurrentUserResponse,
  ChangePasswordRequest,
  ChangePasswordResponse,
  ForgotPasswordRequest,
  ForgotPasswordResponse,
  GoogleLoginRequest,
  GoogleLoginResponse,
  LoginRequest,
  LoginResponse,
  LogoutRequest,
  LogoutResponse,
  RefreshTokenRequest,
  RefreshTokenResponse,
  RegisterRequest,
  RegisterResponse,
  ResendEmailOtpRequest,
  ResendEmailOtpResponse,
  ResetPasswordRequest,
  ResetPasswordResponse,
  VerifyEmailOtpRequest,
  VerifyEmailOtpResponse
} from '../types/auth.types';

export const authApi = {
  register: (payload: RegisterRequest) =>
    apiClient<ApiResponse<RegisterResponse>>(ENDPOINTS.AUTH.REGISTER, {
      method: 'POST',
      body: payload
    }),

  verifyEmailOtp: (payload: VerifyEmailOtpRequest) =>
    apiClient<ApiResponse<VerifyEmailOtpResponse>>(ENDPOINTS.AUTH.VERIFY_EMAIL_OTP, {
      method: 'POST',
      body: payload
    }),

  resendEmailOtp: (payload: ResendEmailOtpRequest) =>
    apiClient<ApiResponse<ResendEmailOtpResponse>>(ENDPOINTS.AUTH.RESEND_EMAIL_OTP, {
      method: 'POST',
      body: payload
    }),

  login: (payload: LoginRequest) =>
    apiClient<ApiResponse<LoginResponse>>(ENDPOINTS.AUTH.LOGIN, {
      method: 'POST',
      body: payload
    }),

  googleLogin: (payload: GoogleLoginRequest) =>
    apiClient<ApiResponse<GoogleLoginResponse>>(ENDPOINTS.AUTH.GOOGLE_LOGIN, {
      method: 'POST',
      body: payload
    }),

  forgotPassword: (payload: ForgotPasswordRequest) =>
    apiClient<ApiResponse<ForgotPasswordResponse>>(ENDPOINTS.AUTH.FORGOT_PASSWORD, {
      method: 'POST',
      body: payload
    }),

  resetPassword: (payload: ResetPasswordRequest) =>
    apiClient<ApiResponse<ResetPasswordResponse>>(ENDPOINTS.AUTH.RESET_PASSWORD, {
      method: 'POST',
      body: payload
    }),

  verifyResetOtp: (payload: { email: string; otp: string }) =>
    apiClient<ApiResponse<{ valid: boolean }>>(ENDPOINTS.AUTH.VERIFY_RESET_OTP, {
      method: 'POST',
      body: payload
    }),

  changePassword: (payload: ChangePasswordRequest) =>
    apiClient<ApiResponse<ChangePasswordResponse>>(ENDPOINTS.AUTH.CHANGE_PASSWORD, {
      method: 'POST',
      auth: true,
      body: payload
    }),

  refreshToken: (payload: RefreshTokenRequest) =>
    apiClient<ApiResponse<RefreshTokenResponse>>(ENDPOINTS.AUTH.REFRESH_TOKEN, {
      method: 'POST',
      body: payload
    }),

  logout: (payload: LogoutRequest) =>
    apiClient<ApiResponse<LogoutResponse>>(ENDPOINTS.AUTH.LOGOUT, {
      method: 'POST',
      body: payload
    }),

  logoutAll: () =>
    apiClient<ApiResponse<LogoutResponse>>(ENDPOINTS.AUTH.LOGOUT_ALL, {
      method: 'POST',
      auth: true
    }),

  getMe: () =>
    apiClient<ApiResponse<CurrentUserResponse>>(ENDPOINTS.USERS.ME, {
      method: 'GET',
      auth: true
    })
};
