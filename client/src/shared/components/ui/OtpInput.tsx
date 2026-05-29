import { useRef } from 'react';

interface OtpInputProps {
  value: string;
  onChange: (otp: string) => void;
  disabled?: boolean;
  error?: boolean;
}

export function OtpInput({ value, onChange, disabled, error }: OtpInputProps) {
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);
  const values = Array(6).fill('');
  for (let i = 0; i < 6; i++) {
    values[i] = value[i] || '';
  }

  const handleChange = (val: string, index: number) => {
    if (disabled) return;
    const digit = val.replace(/[^0-9]/g, '');
    if (!digit) {
      const newValues = [...values];
      newValues[index] = '';
      onChange(newValues.join(''));
      return;
    }

    const newValues = [...values];
    newValues[index] = digit.slice(-1);
    const newCombined = newValues.join('');
    onChange(newCombined);

    // Auto focus next input
    if (index < 5) {
      inputRefs.current[index + 1]?.focus();
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>, index: number) => {
    if (disabled) return;
    if (e.key === 'Backspace') {
      if (!values[index] && index > 0) {
        const newValues = [...values];
        newValues[index - 1] = '';
        onChange(newValues.join(''));
        inputRefs.current[index - 1]?.focus();
      } else {
        const newValues = [...values];
        newValues[index] = '';
        onChange(newValues.join(''));
      }
      e.preventDefault();
    }
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    if (disabled) return;
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
    const newCombined = newValues.join('');
    onChange(newCombined);

    const focusIndex = Math.min(digits.length, 5);
    inputRefs.current[focusIndex]?.focus();
  };

  return (
    <div className="otp-input-container">
      {values.map((val, i) => (
        <input
          key={i}
          ref={el => {
            inputRefs.current[i] = el;
          }}
          type="text"
          inputMode="numeric"
          maxLength={1}
          pattern="[0-9]*"
          value={val}
          disabled={disabled}
          className={`otp-box ${error ? 'has-error' : ''}`}
          onChange={e => handleChange(e.target.value, i)}
          onKeyDown={e => handleKeyDown(e, i)}
          onPaste={handlePaste}
          onFocus={e => e.target.select()}
          aria-label={`Mã OTP số ${i + 1}`}
        />
      ))}
    </div>
  );
}
