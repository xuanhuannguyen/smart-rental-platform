import { useState, type FormEvent } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { Input } from '../../../shared/components/ui/Input';
import type { ResetPasswordRequest } from '../types/auth.types';

interface ResetPasswordFormProps {
  defaultEmail?: string;
  isSubmitting: boolean;
  onSubmit: (payload: ResetPasswordRequest) => void;
}

export function ResetPasswordForm({
  defaultEmail = '',
  isSubmitting,
  onSubmit
}: ResetPasswordFormProps) {
  const [form, setForm] = useState<ResetPasswordRequest>({
    email: defaultEmail,
    otp: '',
    newPassword: ''
  });
  const [errors, setErrors] = useState<Partial<Record<keyof ResetPasswordRequest, string>>>({});

  function validate() {
    const nextErrors: Partial<Record<keyof ResetPasswordRequest, string>> = {};
    const normalizedEmail = form.email.trim();
    const normalizedOtp = form.otp.trim();

    if (!normalizedEmail) {
      nextErrors.email = 'Vui lòng nhập email.';
    } else if (!/^\S+@\S+\.\S+$/.test(normalizedEmail)) {
      nextErrors.email = 'Email không hợp lệ.';
    }

    if (!/^\d{6}$/.test(normalizedOtp)) {
      nextErrors.otp = 'OTP phải gồm 6 chữ số.';
    }

    if (!form.newPassword.trim()) {
      nextErrors.newPassword = 'Vui lòng nhập mật khẩu mới.';
    } else if (form.newPassword.length < 6) {
      nextErrors.newPassword = 'Mật khẩu mới phải có ít nhất 6 ký tự.';
    }

    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!validate()) {
      return;
    }

    onSubmit({
      email: form.email.trim(),
      otp: form.otp.trim(),
      newPassword: form.newPassword
    });
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <FormField label="Email" htmlFor="reset-password-email" error={errors.email}>
        <Input
          id="reset-password-email"
          type="email"
          value={form.email}
          hasError={Boolean(errors.email)}
          disabled={isSubmitting}
          onChange={event => setForm(current => ({ ...current, email: event.target.value }))}
        />
      </FormField>

      <FormField label="Mã OTP" htmlFor="reset-password-otp" error={errors.otp}>
        <Input
          id="reset-password-otp"
          inputMode="numeric"
          maxLength={6}
          value={form.otp}
          hasError={Boolean(errors.otp)}
          disabled={isSubmitting}
          onChange={event => setForm(current => ({ ...current, otp: event.target.value }))}
        />
      </FormField>

      <FormField label="Mật khẩu mới" htmlFor="reset-password-new-password" error={errors.newPassword}>
        <Input
          id="reset-password-new-password"
          type="password"
          value={form.newPassword}
          hasError={Boolean(errors.newPassword)}
          disabled={isSubmitting}
          onChange={event => setForm(current => ({ ...current, newPassword: event.target.value }))}
        />
      </FormField>

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Đang đặt lại mật khẩu...' : 'Đặt lại mật khẩu'}
      </Button>
    </form>
  );
}
