import { useEffect, useRef, useState } from 'react';
import { Button } from '../../../shared/components/ui/Button';
import { Alert } from '../../../shared/components/ui/Alert';

interface WebcamCaptureProps {
  disabled?: boolean;
  onCapture: (file: File | null) => void;
}

export function WebcamCapture({ disabled = false, onCapture }: WebcamCaptureProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [error, setError] = useState<string | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [stream, setStream] = useState<MediaStream | null>(null);

  const startCamera = async () => {
    setError(null);
    try {
      const media = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user' },
        audio: false
      });
      setStream(media);
      if (videoRef.current) {
        videoRef.current.srcObject = media;
      }
    } catch {
      setError('Không thể mở webcam. Vui lòng cấp quyền camera trong cài đặt trình duyệt.');
    }
  };

  const stopCamera = () => {
    if (stream) {
      stream.getTracks().forEach(track => track.stop());
      setStream(null);
    }
  };

  // Start camera on mount
  useEffect(() => {
    void startCamera();
    return () => {
      stopCamera();
    };
  }, []);

  // Stop camera when image is captured to turn off camera light, restart on retake
  useEffect(() => {
    if (previewUrl) {
      stopCamera();
    } else {
      void startCamera();
    }
  }, [previewUrl]);

  useEffect(() => {
    return () => {
      if (previewUrl) {
        URL.revokeObjectURL(previewUrl);
      }
    };
  }, [previewUrl]);

  async function capture() {
    if (!videoRef.current || !stream) {
      return;
    }

    const video = videoRef.current;
    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth || 640;
    canvas.height = video.videoHeight || 480;
    const context = canvas.getContext('2d');

    if (!context) {
      return;
    }

    context.drawImage(video, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>(resolve =>
      canvas.toBlob(value => resolve(value), 'image/jpeg', 0.92)
    );

    if (!blob) {
      return;
    }

    const file = new File([blob], `selfie-${Date.now()}.jpg`, { type: 'image/jpeg' });

    if (previewUrl) {
      URL.revokeObjectURL(previewUrl);
    }

    setPreviewUrl(URL.createObjectURL(blob));
    onCapture(file);
  }

  function handleRetake() {
    if (previewUrl) {
      URL.revokeObjectURL(previewUrl);
      setPreviewUrl(null);
    }
    onCapture(null);
  }

  return (
    <div className="webcam-panel" style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '14px' }}>
      {error ? <Alert type="error">{error}</Alert> : null}
      
      {/* Viewport frame: only 1 frame showing either video stream or captured image preview */}
      <div 
        className="webcam-viewport" 
        style={{ 
          width: '100%', 
          maxWidth: '440px', 
          aspectRatio: '4 / 3', 
          backgroundColor: '#0f172a', 
          borderRadius: '8px', 
          overflow: 'hidden', 
          display: 'flex', 
          alignItems: 'center', 
          justifyContent: 'center',
          border: '1px solid #cbd5e1'
        }}
      >
        {previewUrl ? (
          <img src={previewUrl} alt="Selfie preview" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
        ) : (
          !error && <video ref={videoRef} autoPlay playsInline muted style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
        )}
      </div>

      {previewUrl ? (
        <Button type="button" variant="secondary" disabled={disabled} onClick={handleRetake}>
          Chụp lại
        </Button>
      ) : (
        <Button type="button" disabled={disabled || Boolean(error)} onClick={() => void capture()}>
          Chụp selfie
        </Button>
      )}
    </div>
  );
}
