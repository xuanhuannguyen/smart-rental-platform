import { useCallback, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../../app/providers/AuthProvider';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { GoogleLoginButton } from '../components/GoogleLoginButton';
import { LoginForm } from '../components/LoginForm';
import { authApi } from '../services/authApi';
import type { LoginRequest } from '../types/auth.types';

export function LoginPage() {
  const navigate = useNavigate();
  const { setSession } = useAuth();

  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const finishLogin = useCallback(
    async (accessToken?: string | null, refreshToken?: string | null) => {
      if (!accessToken || !refreshToken) {
        throw new Error('Backend chưa trả token đăng nhập.');
      }

      const user = await setSession(accessToken, refreshToken);

      if (!user) {
        throw new Error('Không lấy được thông tin người dùng hiện tại.');
      }

      if (user.roles.includes('Admin')) {
        navigate(ROUTE_PATHS.ADMIN.ROOT, { replace: true });
        return;
      }

      navigate(user.emailConfirmed ? ROUTE_PATHS.ME.ROOT : ROUTE_PATHS.AUTH.VERIFY_EMAIL, { replace: true });
    },
    [navigate, setSession]
  );

  const handleLocalLogin = useCallback(
    async (payload: LoginRequest) => {
      setIsSubmitting(true);
      setError(null);
      setMessage('Đang đăng nhập...');

      try {
        const response = await authApi.login(payload);
        await finishLogin(response.data.accessToken, response.data.refreshToken);
      } catch (loginError) {
        const errMsg = getApiErrorMessage(loginError, '');
        if (errMsg.includes('Vui lòng xác thực email trước khi đăng nhập.')) {
          const email = encodeURIComponent(payload.email.trim());
          navigate(`${ROUTE_PATHS.AUTH.VERIFY_EMAIL}?email=${email}`, { replace: true });
          return;
        }
        setError(getApiErrorMessage(loginError, 'Đăng nhập thất bại.'));
      } finally {
        setIsSubmitting(false);
      }
    },
    [finishLogin, navigate]
  );

  const handleGoogleCredential = useCallback(
    async (idToken: string) => {
      setIsSubmitting(true);
      setError(null);
      setMessage('Đang xác thực Google...');

      try {
        const response = await authApi.googleLogin({ idToken });
        await finishLogin(response.data.accessToken, response.data.refreshToken);
      } catch (loginError) {
        setError(getApiErrorMessage(loginError, 'Đăng nhập Google thất bại.'));
      } finally {
        setIsSubmitting(false);
      }
    },
    [finishLogin]
  );

  return (
    <main className="auth-page">
      <section className="auth-panel">
        <p className="eyebrow">Smart Rental Platform</p>
        <h1>Đăng nhập</h1>

        {error ? (
          <Alert type="error">{error}</Alert>
        ) : (
          isSubmitting && message && <Alert type="info">{message}</Alert>
        )}

        <LoginForm isSubmitting={isSubmitting} onSubmit={handleLocalLogin} />

        <div className="auth-divider">hoặc</div>

        <GoogleLoginButton onCredential={handleGoogleCredential} />

        <div className="auth-links">
          <Link to={ROUTE_PATHS.AUTH.REGISTER}>Tạo tài khoản</Link>
          <Link to={ROUTE_PATHS.AUTH.FORGOT_PASSWORD}>Quên mật khẩu?</Link>
        </div>
      </section>
    </main>
  );
}
