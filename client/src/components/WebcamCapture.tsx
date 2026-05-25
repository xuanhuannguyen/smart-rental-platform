import { useEffect, useRef, useState } from 'react';

interface WebcamCaptureProps {
  onCapture: (file: File) => void;
}

export default function WebcamCapture({ onCapture }: WebcamCaptureProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    let mediaStream: MediaStream | null = null;

    async function startCamera() {
      try {
        const media = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: 'user' },
          audio: false
        });
        if (!active) {
          media.getTracks().forEach((track) => track.stop());
          return;
        }
        mediaStream = media;
        if (videoRef.current) {
          videoRef.current.srcObject = media;
        }
      } catch {
        setError('Unable to access webcam. Please allow camera permission or upload a selfie file.');
      }
    }

    void startCamera();

    return () => {
      active = false;
      mediaStream?.getTracks().forEach((track) => track.stop());
    };
  }, []);

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  const capture = async () => {
    if (!videoRef.current) return;

    const video = videoRef.current;
    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth || 640;
    canvas.height = video.videoHeight || 480;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob((b) => resolve(b), 'image/jpeg', 0.92)
    );
    if (!blob) return;

    const file = new File([blob], `selfie-${Date.now()}.jpg`, { type: 'image/jpeg' });
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    setPreviewUrl(URL.createObjectURL(blob));
    onCapture(file);
  };

  return (
    <div className="webcam-panel">
      {error ? <p className="error-text">{error}</p> : null}
      {!error ? (
        <video ref={videoRef} autoPlay playsInline muted className="webcam-video" />
      ) : null}
      {previewUrl ? <img src={previewUrl} alt="Selfie preview" className="webcam-preview" /> : null}
      <button type="button" className="btn-secondary" onClick={() => void capture()} disabled={!!error}>
        Capture selfie
      </button>
    </div>
  );
}
