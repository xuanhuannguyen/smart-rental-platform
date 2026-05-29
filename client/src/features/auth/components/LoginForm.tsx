import { useState, type FormEvent } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { Input } from '../../../shared/components/ui/Input';
import type { LoginRequest } from '../types/auth.types';

interface LoginFormProps {
    isSubmitting: boolean;
    onSubmit: (payload: LoginRequest) => void;
}

export function LoginForm({ isSubmitting, onSubmit }: LoginFormProps) {
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
                <Input
                    id="login-password"
                    type="password"
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
            </FormField>

            <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Đang đăng nhập...' : 'Đăng nhập'}
            </Button>
        </form>
    );
}