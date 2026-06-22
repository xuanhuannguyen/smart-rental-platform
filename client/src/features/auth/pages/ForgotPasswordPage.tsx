import { useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { AuthLayout } from '../components/AuthLayout';
import { ForgotPasswordForm } from '../components/ForgotPasswordForm';
import { authApi } from '../services/authApi';
import type { ForgotPasswordRequest } from '../types/auth.types';

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState('Nhập email tài khoản để nhận OTP đặt lại mật khẩu.');
  const [error, setError] = useState<string | null>(null);

  const handleForgotPassword = useCallback(
    async (payload: ForgotPasswordRequest) => {
      setIsSubmitting(true);
      setError(null);
      setMessage('Đang gửi OTP đặt lại mật khẩu...');

      try {
        await authApi.forgotPassword(payload);
        setMessage('OTP đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra email.');
        navigate(`${ROUTE_PATHS.AUTH.RESET_PASSWORD}?email=${encodeURIComponent(payload.email)}`);
      } catch (forgotPasswordError) {
        setError(
          forgotPasswordError instanceof Error
            ? forgotPasswordError.message
            : 'Không thể gửi OTP đặt lại mật khẩu.'
        );
      } finally {
        setIsSubmitting(false);
      }
    },
    [navigate]
  );

  return (
    <AuthLayout
      title="Quên mật khẩu"
      subtitle="Nhập email tài khoản để nhận OTP và tạo mật khẩu mới an toàn."
    >
      {error ? <Alert type="error">{error}</Alert> : <Alert type="info">{message}</Alert>}

      <ForgotPasswordForm isSubmitting={isSubmitting} onSubmit={handleForgotPassword} />

      <div className="auth-links single-link">
        <Link to={ROUTE_PATHS.AUTH.LOGIN}>Quay lại đăng nhập</Link>
      </div>
    </AuthLayout>
  );
}
