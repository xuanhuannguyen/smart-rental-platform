import { useState, useRef, type FormEvent } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';

interface OtpFormProps {
  isSubmitting: boolean;
  submitLabel?: string;
  onSubmit: (otp: string) => void;
}

export function OtpForm({ isSubmitting, submitLabel = 'Xác thực email', onSubmit }: OtpFormProps) {
  const [values, setValues] = useState<string[]>(Array(6).fill(''));
  const [error, setError] = useState<string | undefined>();
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  const handleChange = (val: string, index: number) => {
    const digit = val.replace(/[^0-9]/g, '');
    if (!digit) {
      const newValues = [...values];
      newValues[index] = '';
      setValues(newValues);
      return;
    }

    const newValues = [...values];
    newValues[index] = digit.slice(-1);
    setValues(newValues);
    setError(undefined);

    // Auto focus next input
    if (index < 5) {
      inputRefs.current[index + 1]?.focus();
    }

    // Auto submit if complete
    const combined = newValues.join('');
    if (combined.length === 6 && /^\d{6}$/.test(combined)) {
      onSubmit(combined);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>, index: number) => {
    if (e.key === 'Backspace') {
      if (!values[index] && index > 0) {
        const newValues = [...values];
        newValues[index - 1] = '';
        setValues(newValues);
        inputRefs.current[index - 1]?.focus();
      } else {
        const newValues = [...values];
        newValues[index] = '';
        setValues(newValues);
      }
      e.preventDefault();
    }
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const pastedText = e.clipboardData.getData('text');
    const digits = pastedText.replace(/[^0-9]/g, '').slice(0, 6);
    if (digits.length === 0) return;

    const newValues = [...values];
    for (let i = 0; i < 6; i++) {
      if (i < digits.length) {
        newValues[i] = digits[i];
      }
    }
    setValues(newValues);
    setError(undefined);

    const focusIndex = Math.min(digits.length, 5);
    inputRefs.current[focusIndex]?.focus();

    const combined = newValues.join('');
    if (combined.length === 6 && /^\d{6}$/.test(combined)) {
      onSubmit(combined);
    }
  };

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const combinedOtp = values.join('');

    if (combinedOtp.length < 6 || !/^\d{6}$/.test(combinedOtp)) {
      setError('Vui lòng nhập đầy đủ 6 chữ số OTP.');
      return;
    }

    setError(undefined);
    onSubmit(combinedOtp);
  }

  return (
    <form className="auth-form" onSubmit={handleSubmit}>
      <FormField label="Mã OTP" htmlFor="verify-otp-0" error={error}>
        <div className="otp-input-container">
          {values.map((val, i) => (
            <input
              key={i}
              id={`verify-otp-${i}`}
              ref={el => {
                inputRefs.current[i] = el;
              }}
              type="text"
              inputMode="numeric"
              maxLength={1}
              pattern="[0-9]*"
              value={val}
              disabled={isSubmitting}
              className={`otp-box ${error ? 'has-error' : ''}`}
              onChange={e => handleChange(e.target.value, i)}
              onKeyDown={e => handleKeyDown(e, i)}
              onPaste={handlePaste}
              onFocus={e => e.target.select()}
              aria-label={`Mã OTP số ${i + 1}`}
            />
          ))}
        </div>
      </FormField>


      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Đang xác thực...' : submitLabel}
      </Button>
    </form>
  );
}

