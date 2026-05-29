import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { RegisterForm } from '../components/RegisterForm';
import { authApi } from '../services/authApi';
import type { RegisterRequest } from '../types/auth.types';

export function RegisterPage() {
  const navigate = useNavigate();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleRegister(payload: RegisterRequest) {
    setIsSubmitting(true);
    setError(null);

    try {
      await authApi.register(payload);
      const email = encodeURIComponent(payload.email.trim());
      navigate(`${ROUTE_PATHS.AUTH.VERIFY_EMAIL}?email=${email}`, { replace: true });
    } catch (registerError) {
      setError(getApiErrorMessage(registerError, 'Đăng ký thất bại.'));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-panel">
        <p className="eyebrow">Smart Rental Platform</p>
        <h1>Đăng ký</h1>


        {error && <Alert type="error">{error}</Alert>}

        <RegisterForm isSubmitting={isSubmitting} onSubmit={handleRegister} />

        <div className="auth-links">
          <Link to={ROUTE_PATHS.AUTH.LOGIN}>Đã có tài khoản?</Link>
        </div>
      </section>
    </main>
  );
}
