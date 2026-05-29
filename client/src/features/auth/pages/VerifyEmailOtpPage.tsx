import { useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { OtpForm } from '../components/OtpForm';
import { authApi } from '../services/authApi';

export function VerifyEmailOtpPage() {
  const navigate = useNavigate();
  const { currentUser, refreshMe } = useAuth();
  const [searchParams] = useSearchParams();
  const email = useMemo(() => {
    const urlEmail = searchParams.get('email')?.trim();
    if (urlEmail) return urlEmail;
    return currentUser?.email || '';
  }, [searchParams, currentUser]);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isResending, setIsResending] = useState(false);

  async function handleVerify(otp: string) {
    if (!email) {
      setError('Thiếu email cần xác thực.');
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setMessage(null);

    try {
      await authApi.verifyEmailOtp({ email, otp });
      if (currentUser) {
        await refreshMe();
        navigate(ROUTE_PATHS.ME.ROOT, { replace: true });
      } else {
        navigate(ROUTE_PATHS.AUTH.LOGIN, { replace: true });
      }
    } catch (verifyError) {
      setError(getApiErrorMessage(verifyError, 'Xác thực OTP thất bại.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleResend() {
    if (!email) {
      setError('Thiếu email cần gửi lại OTP.');
      return;
    }

    setIsResending(true);
    setError(null);
    setMessage(null);

    try {
      const response = await authApi.resendEmailOtp({ email });
      setMessage(response.message || 'Nếu email hợp lệ, hệ thống sẽ xử lý yêu cầu gửi lại OTP.');
    } catch (resendError) {
      setError(getApiErrorMessage(resendError, 'Không gửi lại được OTP.'));
    } finally {
      setIsResending(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-panel">
        <p className="eyebrow">Xác thực email</p>
        <h1>Nhập mã OTP</h1>
        <p className="subtle">
          OTP đã được gửi tới <strong>{email || 'email đăng ký'}</strong>. Mã có hiệu lực trong 15 phút.
        </p>

        {error && <Alert type="error">{error}</Alert>}
        {message && <Alert type="success">{message}</Alert>}

        <OtpForm isSubmitting={isSubmitting} onSubmit={handleVerify} />

        <div className="auth-actions">
          <Button type="button" variant="secondary" disabled={isResending} onClick={() => void handleResend()}>
            {isResending ? 'Đang gửi lại...' : 'Gửi lại OTP'}
          </Button>
        </div>

        <div className="auth-links">
          <Link to={ROUTE_PATHS.AUTH.LOGIN}>Quay lại đăng nhập</Link>
        </div>
      </section>
    </main>
  );
}
