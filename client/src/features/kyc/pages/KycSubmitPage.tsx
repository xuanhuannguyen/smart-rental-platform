import { useCallback, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ROUTE_PATHS } from '../../../app/router/routePaths';
import { getApiErrorMessage } from '../../../shared/api/apiError';
import { Alert } from '../../../shared/components/ui/Alert';
import { Button } from '../../../shared/components/ui/Button';
import { FormField } from '../../../shared/components/ui/FormField';
import { WebcamCapture } from '../components/WebcamCapture';
import { kycApi } from '../services/kycApi';
import type {
  KycDocumentType,
  KycSubmissionResponse,
  SelfieCaptureMethod
} from '../types/kyc.types';

const steps = ['Giấy tờ', 'Ảnh CCCD', 'Selfie', 'Kiểm tra'];

function fileLabel(file: File | null) {
  return file ? file.name : 'Chưa chọn';
}

export function KycSubmitPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  const [documentType, setDocumentType] = useState<KycDocumentType>('CCCD');
  const [selfieMethod, setSelfieMethod] = useState<SelfieCaptureMethod>('Webcam');
  const [frontImage, setFrontImage] = useState<File | null>(null);
  const [backImage, setBackImage] = useState<File | null>(null);
  const [selfieImage, setSelfieImage] = useState<File | null>(null);
  const [result, setResult] = useState<KycSubmissionResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      setError(null);
      setResult(null);

      if (!frontImage || !backImage || !selfieImage) {
        setError('Vui lòng chọn đủ ảnh mặt trước, mặt sau và selfie.');
        return;
      }

      setIsSubmitting(true);

      try {
        const response = await kycApi.submit({
          documentType,
          selfieCaptureMethod: selfieMethod,
          frontImage,
          backImage,
          selfieImage
        });
        setResult(response.data);
      } catch (submitError) {
        setError(getApiErrorMessage(submitError, 'Không thể gửi KYC.'));
      } finally {
        setIsSubmitting(false);
      }
    },
    [backImage, documentType, frontImage, selfieImage, selfieMethod]
  );

  return (
    <main className="auth-page">
      <section className="auth-panel kyc-panel">
        <div style={{ marginBottom: '16px', display: 'flex', justifyContent: 'flex-start' }}>
          <Button
            type="button"
            variant="secondary"
            onClick={() => navigate(-1)}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '8px' }}
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth="2.5">
              <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
            </svg>
            Quay lại
          </Button>
        </div>
        <p className="eyebrow">KYC</p>
        <h1>Xác minh danh tính</h1>
        <p className="subtle">
          Gửi mặt trước, mặt sau giấy tờ để kiểm tra qua VNPT. Ảnh selfie được lưu để admin đối chiếu thủ công.
        </p>

        {error ? <Alert type="error">{error}</Alert> : null}
        {result ? (
          <Alert type={result.status === 'EkycFailed' ? 'error' : 'success'}>
            {result.message}
          </Alert>
        ) : null}

        <div className="kyc-stepper">
          {steps.map((label, index) => (
            <button
              key={label}
              type="button"
              className={index === step ? 'kyc-step active' : 'kyc-step'}
              disabled={isSubmitting}
              onClick={() => setStep(index)}
            >
              {index + 1}. {label}
            </button>
          ))}
        </div>

        <form className="auth-form" onSubmit={handleSubmit}>
          {step === 0 ? (
            <FormField label="Loại giấy tờ" htmlFor="kyc-document-type">
              <select
                id="kyc-document-type"
                className="ui-input"
                value={documentType}
                disabled={isSubmitting}
                onChange={event => setDocumentType(event.target.value as KycDocumentType)}
              >
                <option value="CCCD">CCCD</option>
                <option value="Passport">Passport</option>
              </select>
            </FormField>
          ) : null}

          {step === 1 ? (
            <div className="kyc-grid">
              <FormField label="Ảnh mặt trước" htmlFor="kyc-front-image">
                <input
                  id="kyc-front-image"
                  className="ui-input file-input"
                  type="file"
                  accept="image/*"
                  disabled={isSubmitting}
                  onChange={event => setFrontImage(event.target.files?.[0] ?? null)}
                />
              </FormField>
              <FormField label="Ảnh mặt sau" htmlFor="kyc-back-image">
                <input
                  id="kyc-back-image"
                  className="ui-input file-input"
                  type="file"
                  accept="image/*"
                  disabled={isSubmitting}
                  onChange={event => setBackImage(event.target.files?.[0] ?? null)}
                />
              </FormField>
            </div>
          ) : null}

          {step === 2 ? (
            <div className="form-field" style={{ gap: '10px' }}>
              <label style={{ fontWeight: 'bold', fontSize: '14px' }}>Chụp ảnh Selfie bằng Webcam</label>
              <p className="subtle" style={{ margin: 0, fontSize: '13px' }}>
                Vui lòng nhìn thẳng vào camera máy tính và bấm nút chụp ảnh.
              </p>
              <WebcamCapture disabled={isSubmitting} onCapture={setSelfieImage} />
            </div>
          ) : null}

          {step === 3 ? (
            <dl className="user-summary">
              <div>
                <dt>Giấy tờ</dt>
                <dd>{documentType}</dd>
              </div>
              <div>
                <dt>Mặt trước</dt>
                <dd>{fileLabel(frontImage)}</dd>
              </div>
              <div>
                <dt>Mặt sau</dt>
                <dd>{fileLabel(backImage)}</dd>
              </div>
              <div>
                <dt>Selfie</dt>
                <dd>{fileLabel(selfieImage)}</dd>
              </div>
            </dl>
          ) : null}

          {result ? (
            <dl className="user-summary">
              <div>
                <dt>Trạng thái</dt>
                <dd>{result.status}</dd>
              </div>
              <div>
                <dt>eKYC</dt>
                <dd>{result.ekycResult}</dd>
              </div>
              <div>
                <dt>Rủi ro</dt>
                <dd>{result.riskLevel}</dd>
              </div>
              {result.ocrFullName ? (
                <div>
                  <dt>Họ tên OCR</dt>
                  <dd>{result.ocrFullName}</dd>
                </div>
              ) : null}
              {result.ocrCitizenIdMasked ? (
                <div>
                  <dt>Số CCCD OCR</dt>
                  <dd>{result.ocrCitizenIdMasked}</dd>
                </div>
              ) : null}
              {result.ocrDateOfBirth ? (
                <div>
                  <dt>Ngày sinh OCR</dt>
                  <dd>{new Date(result.ocrDateOfBirth).toLocaleDateString()}</dd>
                </div>
              ) : null}
              {result.ocrGender ? (
                <div>
                  <dt>Giới tính OCR</dt>
                  <dd>{result.ocrGender}</dd>
                </div>
              ) : null}
              {result.ocrAddress ? (
                <div>
                  <dt>Địa chỉ OCR</dt>
                  <dd>{result.ocrAddress}</dd>
                </div>
              ) : null}
            </dl>
          ) : null}

          <div className="auth-actions">
            <Button
              type="button"
              variant="secondary"
              disabled={step === 0 || isSubmitting}
              onClick={() => setStep(current => Math.max(0, current - 1))}
            >
              Quay lại
            </Button>
            {step < steps.length - 1 ? (
              <Button
                type="button"
                disabled={isSubmitting}
                onClick={() => setStep(current => Math.min(steps.length - 1, current + 1))}
              >
                Tiếp tục
              </Button>
            ) : (
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Đang gửi KYC...' : 'Gửi KYC'}
              </Button>
            )}
          </div>
        </form>

        <div className="auth-links single-link">
          <Link to={ROUTE_PATHS.ME.KYC_STATUS}>Xem trạng thái KYC</Link>
        </div>
      </section>
    </main>
  );
}
