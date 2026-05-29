import { useCallback, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Alert } from '../../../shared/components/ui/Alert';
import { ResetPasswordForm } from '../components/ResetPasswordForm';
import { authApi } from '../services/authApi';
import type { ResetPasswordRequest } from '../types/auth.types';

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const defaultEmail = useMemo(() => searchParams.get('email') ?? '', [searchParams]);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState('Nhập OTP trong email và mật khẩu mới.');
  const [error, setError] = useState<string | null>(null);

  const handleResetPassword = useCallback(
    async (payload: ResetPasswordRequest) => {
      setIsSubmitting(true);
      setError(null);
      setMessage('Đang đặt lại mật khẩu...');

      try {
        await authApi.resetPassword(payload);
        setMessage('Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.');
        navigate(ROUTE_PATHS.AUTH.LOGIN, { replace: true });
      } catch (resetPasswordError) {
        setError(
          resetPasswordError instanceof Error
            ? resetPasswordError.message
            : 'Không thể đặt lại mật khẩu.'
        );
      } finally {
        setIsSubmitting(false);
      }
    },
    [navigate]
  );

  return (
    <main className="auth-page">
      <section className="auth-panel">
        <p className="eyebrow">Smart Rental Platform</p>
        <h1>Đặt lại mật khẩu</h1>
        <p className="subtle">Dùng OTP đã nhận qua email để xác nhận mật khẩu mới.</p>

        {error ? <Alert type="error">{error}</Alert> : <Alert type="info">{message}</Alert>}

        <ResetPasswordForm
          defaultEmail={defaultEmail}
          isSubmitting={isSubmitting}
          onSubmit={handleResetPassword}
        />

        <div className="auth-links">
          <Link to={ROUTE_PATHS.AUTH.FORGOT_PASSWORD}>Gửi lại OTP</Link>
          <Link to={ROUTE_PATHS.AUTH.LOGIN}>Quay lại đăng nhập</Link>
        </div>
      </section>
    </main>
  );
}
