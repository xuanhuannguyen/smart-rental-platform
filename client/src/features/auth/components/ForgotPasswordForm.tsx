import { useState, type FormEvent } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { Input } from '../../../shared/components/ui/Input';
import type { ForgotPasswordRequest } from '../types/auth.types';

interface ForgotPasswordFormProps {
  isSubmitting: boolean;
  onSubmit: (payload: ForgotPasswordRequest) => void;
}

export function ForgotPasswordForm({ isSubmitting, onSubmit }: ForgotPasswordFormProps) {
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | undefined>();

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const normalizedEmail = email.trim();

    if (!normalizedEmail) {
      setError('Vui lòng nhập email.');
      return;
    }

    if (!/^\S+@\S+\.\S+$/.test(normalizedEmail)) {
      setError('Email không hợp lệ.');
      return;
    }

    setError(undefined);
    onSubmit({ email: normalizedEmail });
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <FormField label="Email" htmlFor="forgot-password-email" error={error}>
        <Input
          id="forgot-password-email"
          type="email"
          value={email}
          hasError={Boolean(error)}
          disabled={isSubmitting}
          onChange={event => setEmail(event.target.value)}
        />
      </FormField>

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Đang gửi OTP...' : 'Gửi OTP đặt lại mật khẩu'}
      </Button>
    </form>
  );
}
