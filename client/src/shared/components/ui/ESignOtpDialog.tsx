import React, { useState, useRef, useCallback } from "react";
import "./ESignOtpDialog.css";

export interface ESignOtpDialogProps {
  isOpen: boolean;
  signerName: string;
  /** If true, a signature image field is shown (submit OTP phase) */
  requiresSignatureImage?: boolean;
  /** Loading state while sending OTP */
  isSendingOtp?: boolean;
  /** Loading state while submitting OTP + image */
  isSubmitting?: boolean;
  error?: string | null;
  onRequestOtp: () => void;
  onSubmit: (otp: string, signatureImageBase64?: string) => void;
  onClose: () => void;
}

export const ESignOtpDialog: React.FC<ESignOtpDialogProps> = ({
  isOpen,
  signerName,
  requiresSignatureImage = false,
  isSendingOtp = false,
  isSubmitting = false,
  error,
  onRequestOtp,
  onSubmit,
  onClose,
}) => {
  const [otp, setOtp] = useState("");
  const [signatureImageBase64, setSignatureImageBase64] = useState<string>("");
  const [otpSent, setOtpSent] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleRequestOtp = useCallback(async () => {
    onRequestOtp();
    setOtpSent(true);
  }, [onRequestOtp]);

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onloadend = () => {
        const result = reader.result as string;
        const base64 = result.includes(",") ? result.split(",")[1] : result;
        setSignatureImageBase64(base64);
      };
      reader.readAsDataURL(file);
    },
    []
  );

  const handleSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      onSubmit(
        otp.trim(),
        requiresSignatureImage ? signatureImageBase64 : undefined
      );
    },
    [otp, signatureImageBase64, requiresSignatureImage, onSubmit]
  );

  const handleClose = useCallback(() => {
    if (isSendingOtp || isSubmitting) return;
    setOtp("");
    setSignatureImageBase64("");
    setOtpSent(false);
    if (fileInputRef.current) fileInputRef.current.value = "";
    onClose();
  }, [isSendingOtp, isSubmitting, onClose]);

  if (!isOpen) return null;

  const isLoading = isSendingOtp || isSubmitting;

  return (
    <div
      className="esign-otp-overlay"
      onClick={(e) => e.target === e.currentTarget && handleClose()}
    >
      <div
        className="esign-otp-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="esign-otp-title"
      >
        <div className="esign-otp-header">
          <h2 id="esign-otp-title">Ký điện tử hợp đồng</h2>
          <button
            type="button"
            className="esign-otp-close"
            aria-label="Đóng"
            disabled={isLoading}
            onClick={handleClose}
          >
            &times;
          </button>
        </div>

        <div className="esign-otp-content">
          {error && (
            <div
              className="esign-otp-alert"
              role="alert"
              style={{
                color: "#dc2626",
                background: "#fef2f2",
                border: "1px solid #fecaca",
                borderRadius: 8,
                padding: "10px 14px",
                marginBottom: 16,
                fontSize: "0.9rem",
              }}
            >
              {error}
            </div>
          )}

          <p className="esign-otp-description">
            Xin chào <strong>{signerName}</strong>. Để ký điện tử, vui lòng yêu
            cầu mã OTP gửi về email của bạn, sau đó nhập mã và
            {requiresSignatureImage
              ? " tải lên chữ ký hình ảnh."
              : " xác nhận."}
          </p>

          <form onSubmit={handleSubmit} noValidate>
            {!otpSent ? (
              <div className="esign-otp-actions">
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={handleClose}
                  disabled={isLoading}
                >
                  Hủy
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  onClick={handleRequestOtp}
                  disabled={isSendingOtp}
                >
                  {isSendingOtp ? "Đang gửi..." : "Gửi OTP"}
                </button>
              </div>
            ) : (
              <>
                <div className="esign-otp-field">
                  <label htmlFor="esign-otp-input" className="esign-otp-label">
                    Mã OTP *
                  </label>
                  <input
                    id="esign-otp-input"
                    type="text"
                    inputMode="numeric"
                    pattern="[0-9]*"
                    maxLength={8}
                    className="esign-otp-control"
                    placeholder="Nhập mã OTP"
                    value={otp}
                    onChange={(e) =>
                      setOtp(e.target.value.replace(/\D/g, ""))
                    }
                    disabled={isLoading}
                    autoComplete="one-time-code"
                    required
                  />
                </div>

                {requiresSignatureImage && (
                  <div className="esign-otp-field">
                    <label
                      htmlFor="esign-signature-file"
                      className="esign-otp-label"
                    >
                      Chữ ký hình ảnh *
                    </label>
                    <input
                      id="esign-signature-file"
                      ref={fileInputRef}
                      type="file"
                      accept="image/png,image/jpeg,image/jpg"
                      className="esign-otp-file"
                      onChange={handleFileChange}
                      disabled={isLoading}
                    />
                  </div>
                )}

                <div className="esign-otp-actions">
                  <button
                    type="button"
                    className="btn-secondary"
                    onClick={handleRequestOtp}
                    disabled={isLoading}
                  >
                    {isSendingOtp ? "Đang gửi..." : "Gửi lại OTP"}
                  </button>
                  <button
                    type="submit"
                    className="btn-primary"
                    disabled={
                      isLoading ||
                      !otp ||
                      (requiresSignatureImage && !signatureImageBase64)
                    }
                  >
                    {isSubmitting ? "Đang xử lý..." : "Xác nhận ký"}
                  </button>
                </div>
              </>
            )}
          </form>
        </div>
      </div>
    </div>
  );
};
