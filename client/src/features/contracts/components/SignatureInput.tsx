import { useRef, useState, type ChangeEvent, type PointerEvent } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { SIGNATURE_IMAGE_ACCEPT, validateSignatureImageFile } from '../signatureImage';
import './SignatureInput.css';

type SignatureInputMode = 'draw' | 'upload';

interface SignatureInputProps {
  idPrefix: string;
  disabled?: boolean;
  value: string;
  onChange: (base64: string) => void;
  onError: (message: string) => void;
}

const CANVAS_WIDTH = 900;
const CANVAS_HEIGHT = 300;
const STROKE_WIDTH = 5;

export function SignatureInput({ idPrefix, disabled = false, value, onChange, onError }: SignatureInputProps) {
  const [mode, setMode] = useState<SignatureInputMode>('draw');
  const [isDrawing, setIsDrawing] = useState(false);
  const [hasDrawing, setHasDrawing] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const lastPointRef = useRef<{ x: number; y: number } | null>(null);

  const getCanvasPoint = (event: PointerEvent<HTMLCanvasElement>) => {
    const canvas = event.currentTarget;
    const rect = canvas.getBoundingClientRect();
    return {
      x: ((event.clientX - rect.left) / rect.width) * CANVAS_WIDTH,
      y: ((event.clientY - rect.top) / rect.height) * CANVAS_HEIGHT
    };
  };

  const getContext = () => {
    const canvas = canvasRef.current;
    if (!canvas) return null;

    const context = canvas.getContext('2d');
    if (!context) return null;

    context.lineWidth = STROKE_WIDTH;
    context.lineCap = 'round';
    context.lineJoin = 'round';
    context.strokeStyle = '#111111';
    context.fillStyle = '#111111';
    return context;
  };

  const exportCanvas = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    onChange(canvas.toDataURL('image/png'));
  };

  const handleModeChange = (nextMode: SignatureInputMode) => {
    setMode(nextMode);
    handleClear();
  };

  const handlePointerDown = (event: PointerEvent<HTMLCanvasElement>) => {
    if (disabled) return;

    const context = getContext();
    if (!context) return;

    const point = getCanvasPoint(event);
    event.currentTarget.setPointerCapture(event.pointerId);
    context.beginPath();
    context.arc(point.x, point.y, STROKE_WIDTH / 2, 0, Math.PI * 2);
    context.fill();
    lastPointRef.current = point;
    setIsDrawing(true);
    setHasDrawing(true);
    exportCanvas();
  };

  const handlePointerMove = (event: PointerEvent<HTMLCanvasElement>) => {
    if (!isDrawing || disabled) return;

    const context = getContext();
    const previousPoint = lastPointRef.current;
    if (!context || !previousPoint) return;

    const point = getCanvasPoint(event);
    context.beginPath();
    context.moveTo(previousPoint.x, previousPoint.y);
    context.lineTo(point.x, point.y);
    context.stroke();
    lastPointRef.current = point;
  };

  const handlePointerUp = (event: PointerEvent<HTMLCanvasElement>) => {
    if (!isDrawing) return;

    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }

    setIsDrawing(false);
    lastPointRef.current = null;
    exportCanvas();
  };

  const handlePointerCancel = () => {
    if (!isDrawing) return;

    setIsDrawing(false);
    lastPointRef.current = null;
    exportCanvas();
  };

  const handleClear = () => {
    const canvas = canvasRef.current;
    const context = canvas?.getContext('2d');
    if (canvas && context) {
      context.clearRect(0, 0, CANVAS_WIDTH, CANVAS_HEIGHT);
    }

    setHasDrawing(false);
    onChange('');
  };

  const handleSignatureImageChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const validationError = validateSignatureImageFile(file);
    if (validationError) {
      onChange('');
      onError(validationError);
      event.target.value = '';
      return;
    }

    const reader = new FileReader();
    reader.onload = () => onChange(String(reader.result ?? ''));
    reader.onerror = () => onError('Không thể đọc ảnh chữ ký.');
    reader.readAsDataURL(file);
  };

  return (
    <>
      <div className="esign-otp-field">
        <label className="esign-otp-label" htmlFor={`${idPrefix}-signature-mode`}>Cách nhập chữ ký</label>
        <select
          id={`${idPrefix}-signature-mode`}
          className="esign-otp-control"
          value={mode}
          onChange={(event) => handleModeChange(event.target.value as SignatureInputMode)}
          disabled={disabled}
        >
          <option value="draw">Vẽ chữ ký</option>
          <option value="upload">Tải ảnh chữ ký</option>
        </select>
      </div>

      {mode === 'draw' ? (
        <div className="esign-otp-field">
          <div className="signature-draw-header">
            <label className="esign-otp-label" htmlFor={`${idPrefix}-signature-canvas`}>Chữ ký</label>
            <Button
              type="button"
              variant="outline"
              className="signature-draw-clear"
              onClick={handleClear}
              disabled={disabled || !hasDrawing}
            >
              Xóa
            </Button>
          </div>
          <canvas
            ref={canvasRef}
            id={`${idPrefix}-signature-canvas`}
            className="signature-draw-canvas"
            width={CANVAS_WIDTH}
            height={CANVAS_HEIGHT}
            aria-label="Vùng vẽ chữ ký"
            onPointerDown={handlePointerDown}
            onPointerMove={handlePointerMove}
            onPointerUp={handlePointerUp}
            onPointerLeave={handlePointerCancel}
            onPointerCancel={handlePointerCancel}
          />
          {!value && <p className="signature-input-hint">Vẽ chữ ký trong khung trước khi gửi OTP.</p>}
        </div>
      ) : (
        <div className="esign-otp-field">
          <label className="esign-otp-label" htmlFor={`${idPrefix}-signature-image`}>Ảnh chữ ký</label>
          <input
            id={`${idPrefix}-signature-image`}
            className="esign-otp-file"
            type="file"
            accept={SIGNATURE_IMAGE_ACCEPT}
            onChange={handleSignatureImageChange}
            disabled={disabled}
          />
        </div>
      )}
    </>
  );
}
