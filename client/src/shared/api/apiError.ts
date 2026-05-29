import type { ApiErrorResponse } from './apiResponse.types';

const ERROR_MESSAGES: Record<string, string> = {
  UNAUTHORIZED: 'Bạn cần đăng nhập để tiếp tục.',
  INVALID_EMAIL_OR_PASSWORD: 'Email hoặc mật khẩu không đúng.',
  EMAIL_ALREADY_EXISTS: 'Email này đã được đăng ký.',
  GOOGLE_ACCOUNT_EXISTS: 'Email này đã đăng nhập bằng Google. Vui lòng dùng Google hoặc đặt mật khẩu.',
  GOOGLE_ID_TOKEN_INVALID: 'Phiên đăng nhập Google không hợp lệ.',
  USER_LOCKED: 'Tài khoản đang bị khóa tạm thời.',
  USER_BANNED: 'Tài khoản đã bị khóa bởi hệ thống.',
  USER_DELETED: 'Tài khoản không còn tồn tại.',
  OTP_INVALID: 'Mã OTP không đúng.',
  OTP_EXPIRED: 'Mã OTP đã hết hạn.',
  OTP_RESEND_TOO_SOON: 'Bạn vừa yêu cầu OTP. Vui lòng thử lại sau.',
};

export class ApiClientError extends Error {
  readonly errorCode?: string;
  readonly details?: unknown;
  readonly status?: number;
  readonly response?: ApiErrorResponse | null;

  constructor(message: string, options: {
    errorCode?: string;
    details?: unknown;
    status?: number;
    response?: ApiErrorResponse | null;
  } = {}) {
    super(message);
    this.name = 'ApiClientError';
    this.errorCode = options.errorCode;
    this.details = options.details;
    this.status = options.status;
    this.response = options.response;
  }
}

export function getApiErrorMessage(error: unknown, fallback = 'Đã xảy ra lỗi. Vui lòng thử lại.'): string {
  if (error && typeof error === 'object' && 'errorCode' in error) {
    const apiError = error as ApiErrorResponse;
    return ERROR_MESSAGES[apiError.errorCode] || apiError.message || fallback;
  }

  if (error instanceof Error) {
    return error.message || fallback;
  }

  return fallback;
}
