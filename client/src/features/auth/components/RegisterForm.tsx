import { useState, type FormEvent } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { Input } from '../../../shared/components/ui/Input';
import type { RegisterRequest } from '../types/auth.types';

interface RegisterFormProps {
  isSubmitting: boolean;
  onSubmit: (payload: RegisterRequest) => void;
}

export function RegisterForm({ isSubmitting, onSubmit }: RegisterFormProps) {
  const [form, setForm] = useState<RegisterRequest>({
    email: '',
    password: '',
    displayName: '',
    phoneNumber: ''
  });
  const [confirmPassword, setConfirmPassword] = useState('');
  const [errors, setErrors] = useState<Partial<Record<keyof RegisterRequest | 'confirmPassword', string>>>({});

  function getErrors() {
    const nextErrors: Partial<Record<keyof RegisterRequest | 'confirmPassword', string>> = {};

    if (!form.email.trim()) {
      nextErrors.email = 'Vui lòng nhập email.';
    } else if (!/^\S+@\S+\.\S+$/.test(form.email)) {
      nextErrors.email = 'Email không hợp lệ.';
    }

    const pwd = form.password;
    if (!pwd) {
      nextErrors.password = 'Vui lòng nhập mật khẩu.';
    } else if (pwd.length < 8) {
      nextErrors.password = 'Mật khẩu phải chứa ít nhất 8 ký tự.';
    } else if (!/[A-Z]/.test(pwd)) {
      nextErrors.password = 'Mật khẩu phải chứa ít nhất 1 chữ viết hoa.';
    } else if (!/[a-z]/.test(pwd)) {
      nextErrors.password = 'Mật khẩu phải chứa ít nhất 1 chữ viết thường.';
    } else if (!/[0-9]/.test(pwd)) {
      nextErrors.password = 'Mật khẩu phải chứa ít nhất 1 chữ số.';
    } else if (!/[!@#$%^&*(),.?":{}|<>]/.test(pwd)) {
      nextErrors.password = 'Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt (ví dụ: !@#$%^&*).';
    }

    if (!confirmPassword) {
      nextErrors.confirmPassword = 'Vui lòng nhập lại mật khẩu.';
    } else if (pwd !== confirmPassword) {
      nextErrors.confirmPassword = 'Mật khẩu nhập lại không khớp.';
    }

    if (!form.displayName.trim()) {
      nextErrors.displayName = 'Vui lòng nhập tên hiển thị.';
    }

    const phone = form.phoneNumber?.trim();
    if (!phone) {
      nextErrors.phoneNumber = 'Vui lòng nhập số điện thoại.';
    } else if (!/^(0|\+84)(3|5|7|8|9)[0-9]{8}$/.test(phone)) {
      nextErrors.phoneNumber = 'Số điện thoại không đúng định dạng Việt Nam (ví dụ: 03..., 09...).';
    }

    return nextErrors;
  }

  function validate() {
    const nextErrors = getErrors();
    setErrors(nextErrors);
    return Object.keys(nextErrors).length === 0;
  }

  function handleBlur(field: keyof RegisterRequest | 'confirmPassword') {
    const nextErrors = getErrors();
    setErrors(current => {
      const updatedErrors = { ...current, [field]: nextErrors[field] };
      // Always sync confirmPassword validation when password is blurred to update mismatch errors
      if (field === 'password' && confirmPassword !== undefined) {
        updatedErrors.confirmPassword = nextErrors.confirmPassword;
      }
      return updatedErrors;
    });
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!validate()) {
      return;
    }

    onSubmit({
      email: form.email.trim(),
      password: form.password,
      displayName: form.displayName.trim(),
      phoneNumber: form.phoneNumber?.trim() || undefined
    });
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <FormField label="Email" htmlFor="register-email" error={errors.email}>
        <Input
          id="register-email"
          type="email"
          value={form.email}
          hasError={Boolean(errors.email)}
          disabled={isSubmitting}
          onChange={event => setForm(current => ({ ...current, email: event.target.value }))}
          onBlur={() => handleBlur('email')}
        />
      </FormField>

      <FormField label="Mật khẩu" htmlFor="register-password" error={errors.password}>
        <Input
          id="register-password"
          type="password"
          value={form.password}
          hasError={Boolean(errors.password)}
          disabled={isSubmitting}
          onChange={event => setForm(current => ({ ...current, password: event.target.value }))}
          onBlur={() => handleBlur('password')}
        />
        <p className="subtle" style={{ fontSize: '0.85rem', marginTop: '4px', marginBottom: 0 }}>
          Mật khẩu cần ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt.
        </p>
      </FormField>

      <FormField label="Nhập lại mật khẩu" htmlFor="register-confirm-password" error={errors.confirmPassword}>
        <Input
          id="register-confirm-password"
          type="password"
          value={confirmPassword}
          hasError={Boolean(errors.confirmPassword)}
          disabled={isSubmitting}
          onChange={event => setConfirmPassword(event.target.value)}
          onBlur={() => handleBlur('confirmPassword')}
        />
      </FormField>

      <FormField label="Tên hiển thị" htmlFor="register-display-name" error={errors.displayName}>
        <Input
          id="register-display-name"
          value={form.displayName}
          hasError={Boolean(errors.displayName)}
          disabled={isSubmitting}
          onChange={event => setForm(current => ({ ...current, displayName: event.target.value }))}
          onBlur={() => handleBlur('displayName')}
        />
      </FormField>

      <FormField label="Số điện thoại" htmlFor="register-phone" error={errors.phoneNumber}>
        <Input
          id="register-phone"
          value={form.phoneNumber}
          hasError={Boolean(errors.phoneNumber)}
          disabled={isSubmitting}
          onChange={event => setForm(current => ({ ...current, phoneNumber: event.target.value }))}
          onBlur={() => handleBlur('phoneNumber')}
        />
      </FormField>

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Đang tạo tài khoản...' : 'Tạo tài khoản'}
      </Button>
    </form>
  );
}
