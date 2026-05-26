import { FormEvent, useState } from 'react';
import { isAxiosError } from 'axios';
import { setDevUserId } from '../api/apiClient';
import { submitKyc, type DocumentType, type SelfieCaptureMethod } from '../api/kycApi';
import WebcamCapture from '../components/WebcamCapture';

const steps = ['Document', 'Front & Back', 'Selfie', 'Review'];

export default function KycSubmitPage() {
  const [step, setStep] = useState(0);
  const [documentType, setDocumentType] = useState<DocumentType>('CCCD');
  const [selfieMethod, setSelfieMethod] = useState<SelfieCaptureMethod>('Webcam');
  const [frontImage, setFrontImage] = useState<File | null>(null);
  const [backImage, setBackImage] = useState<File | null>(null);
  const [selfieImage, setSelfieImage] = useState<File | null>(null);
  const [devUserId, setDevUserIdState] = useState(
    () => localStorage.getItem('srp_dev_user_id') ?? ''
  );
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setMessage(null);

    if (!devUserId) {
      setError('Dev User ID is required until JWT auth is integrated.');
      return;
    }

    if (!frontImage || !backImage || !selfieImage) {
      setError('Please complete all image steps before submitting.');
      return;
    }

    setDevUserId(devUserId);
    setSubmitting(true);

    try {
      const response = await submitKyc({
        documentType,
        selfieCaptureMethod: selfieMethod,
        frontImage,
        backImage,
        selfieImage
      });
      setMessage(response.message || 'KYC submitted successfully.');
    } catch (err) {
      if (isAxiosError(err) && err.response?.data) {
        const payload = err.response.data as { message?: string; code?: string };
        setError(`${payload.code ?? 'ERROR'}: ${payload.message ?? 'Submission failed'}`);
      } else {
        setError('Submission failed. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="card">
      <h2>Identity verification (KYC)</h2>
      <p className="muted">Submit CCCD/Passport images and a live selfie for VNPT eKYC processing.</p>

      <label className="field">
        Dev User ID (temporary until JWT)
        <input
          value={devUserId}
          onChange={(e) => setDevUserIdState(e.target.value)}
          placeholder="00000000-0000-0000-0000-000000000001"
        />
      </label>

      <div className="stepper">
        {steps.map((label, index) => (
          <button
            key={label}
            type="button"
            className={index === step ? 'step active' : 'step'}
            onClick={() => setStep(index)}
          >
            {index + 1}. {label}
          </button>
        ))}
      </div>

      <form onSubmit={onSubmit}>
        {step === 0 ? (
          <label className="field">
            Document type
            <select
              value={documentType}
              onChange={(e) => setDocumentType(e.target.value as DocumentType)}
            >
              <option value="CCCD">CCCD</option>
              <option value="Passport">Passport</option>
            </select>
          </label>
        ) : null}

        {step === 1 ? (
          <div className="grid-2">
            <label className="field">
              Front image
              <input
                type="file"
                accept="image/*"
                onChange={(e) => setFrontImage(e.target.files?.[0] ?? null)}
              />
            </label>
            <label className="field">
              Back image
              <input
                type="file"
                accept="image/*"
                onChange={(e) => setBackImage(e.target.files?.[0] ?? null)}
              />
            </label>
          </div>
        ) : null}

        {step === 2 ? (
          <>
            <label className="field">
              Selfie capture method
              <select
                value={selfieMethod}
                onChange={(e) => setSelfieMethod(e.target.value as SelfieCaptureMethod)}
              >
                <option value="Webcam">Webcam</option>
                <option value="MobileCamera">MobileCamera</option>
                <option value="Upload">Upload</option>
              </select>
            </label>

            {selfieMethod === 'Webcam' ? (
              <WebcamCapture onCapture={setSelfieImage} />
            ) : (
              <label className="field">
                Selfie file
                <input
                  type="file"
                  accept="image/*"
                  onChange={(e) => setSelfieImage(e.target.files?.[0] ?? null)}
                />
              </label>
            )}
          </>
        ) : null}

        {step === 3 ? (
          <ul className="review-list">
            <li>Document: {documentType}</li>
            <li>Front: {frontImage?.name ?? 'Missing'}</li>
            <li>Back: {backImage?.name ?? 'Missing'}</li>
            <li>Selfie: {selfieImage?.name ?? 'Missing'} ({selfieMethod})</li>
          </ul>
        ) : null}

        <div className="actions">
          <button
            type="button"
            className="btn-secondary"
            disabled={step === 0}
            onClick={() => setStep((s) => Math.max(0, s - 1))}
          >
            Back
          </button>
          {step < steps.length - 1 ? (
            <button type="button" className="btn-primary" onClick={() => setStep((s) => s + 1)}>
              Next
            </button>
          ) : (
            <button type="submit" className="btn-primary" disabled={submitting}>
              {submitting ? 'Submitting...' : 'Submit KYC'}
            </button>
          )}
        </div>
      </form>

      {message ? <p className="success-text">{message}</p> : null}
      {error ? <p className="error-text">{error}</p> : null}
    </section>
  );
}
