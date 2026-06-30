import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { Input } from '../../../shared/components/ui/Input';
import type { LoginRequest } from '../types/auth.types';

interface LoginFormProps {
    isSubmitting: boolean;
    onSubmit: (payload: LoginRequest) => void;
}

export function LoginForm({ isSubmitting, onSubmit }: LoginFormProps) {
    const [rememberMe, setRememberMe] = useState(true);
    const [showPassword, setShowPassword] = useState(false);
    const [form, setForm] = useState<LoginRequest>({
        email: '',
        password: ''
    });

    const [errors, setErrors] = useState<Partial<Record<keyof LoginRequest, string>>>({});

    function validate() {
        const nextErrors: Partial<Record<keyof LoginRequest, string>> = {};

        if (!form.email.trim()) {
            nextErrors.email = 'Vui lòng nhập email.';
        } else if (!/^\S+@\S+\.\S+$/.test(form.email)) {
            nextErrors.email = 'Email không hợp lệ.';
        }

        if (!form.password.trim()) {
            nextErrors.password = 'Vui lòng nhập mật khẩu.';
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
            password: form.password
        });
    }

    return (
        <form className="auth-form" onSubmit={handleSubmit}>
            <FormField label="Email" htmlFor="login-email" error={errors.email}>
                <Input
                    id="login-email"
                    type="email"
                    placeholder="Nhập email của bạn"
                    className="auth-input-icon auth-input-email"
                    value={form.email}
                    hasError={Boolean(errors.email)}
                    disabled={isSubmitting}
                    onChange={event =>
                        setForm(current => ({
                            ...current,
                            email: event.target.value
                        }))
                    }
                />
            </FormField>

            <FormField label="Mật khẩu" htmlFor="login-password" error={errors.password}>
                <div className="auth-password-field">
                    <Input
                        id="login-password"
                        type={showPassword ? 'text' : 'password'}
                        placeholder="Nhập mật khẩu"
                        className="auth-input-icon auth-input-password"
                        value={form.password}
                        hasError={Boolean(errors.password)}
                        disabled={isSubmitting}
                        onChange={event =>
                            setForm(current => ({
                                ...current,
                                password: event.target.value
                            }))
                        }
                    />
                    <button
                        className="auth-password-toggle"
                        type="button"
                        aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                        onClick={() => setShowPassword(current => !current)}
                    >
                        {showPassword ? (
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="auth-password-toggle-icon">
                                <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                                <line x1="1" y1="1" x2="23" y2="23" />
                            </svg>
                        ) : (
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="auth-password-toggle-icon">
                                <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
                                <circle cx="12" cy="12" r="3" />
                            </svg>
                        )}
                    </button>
                </div>
            </FormField>

            <div className="auth-form-row">
                <label className="auth-checkbox">
                    <input
                        type="checkbox"
                        checked={rememberMe}
                        disabled={isSubmitting}
                        onChange={event => setRememberMe(event.target.checked)}
                    />
                    <span>Ghi nhớ đăng nhập</span>
                </label>
                <Link to={ROUTE_PATHS.AUTH.FORGOT_PASSWORD}>Quên mật khẩu?</Link>
            </div>

            <Button type="submit" disabled={isSubmitting} className="auth-submit-button">
                <span>{isSubmitting ? 'Đang đăng nhập...' : 'Đăng nhập'}</span>
                <span aria-hidden="true">→</span>
            </Button>
        </form>
    );
}
