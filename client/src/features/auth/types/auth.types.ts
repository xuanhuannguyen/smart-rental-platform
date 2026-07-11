export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  phoneNumber?: string;
}

export interface VerifyEmailOtpRequest {
  email: string;
  otp: string;
}

export interface ResendEmailOtpRequest {
  email: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  otp: string;
  newPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface GoogleLoginRequest {
  idToken: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface LogoutRequest {
  refreshToken: string;
}

export interface AuthUser {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl?: string | null;
  avatarMediaAssetId?: string | null;
  isGoogleUser?: boolean;
  emailConfirmed: boolean;
  requiresEmailVerification?: boolean;
  status: string;
  onboardingStatus: string;
  roles: string[];
  accessToken?: string | null;
  refreshToken?: string | null;
}

export interface RegisterResponse {
  userId: string;
  email: string;
  emailConfirmed: boolean;
  status: string;
  onboardingStatus: string;
  roles: string[];
}

export interface VerifyEmailOtpResponse {
  userId: string;
  email: string;
  emailConfirmed: boolean;
}

export interface ResendEmailOtpResponse {
  email: string;
  emailConfirmed: boolean;
  otpSent: boolean;
}

export interface ForgotPasswordResponse {
  email: string;
}

export interface ResetPasswordResponse {
  email: string;
  emailConfirmed: boolean;
  revokedRefreshTokenCount: number;
}

export interface ChangePasswordResponse {
  passwordChanged: boolean;
  revokedRefreshTokenCount: number;
}

export interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
}

export interface LogoutResponse {
  revokedTokenCount: number;
}

export type LoginResponse = AuthUser;

export type GoogleLoginResponse = AuthUser;

export type CurrentUserResponse = AuthUser;
