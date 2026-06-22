import { useEffect, useRef, useState } from 'react';
import { env } from '../../../config/env';

declare global {
  interface Window {
    google?: {
      accounts: {
        id: {
          initialize: (options: {
            client_id: string;
            callback: (response: { credential: string }) => void;
          }) => void;
          renderButton: (element: HTMLElement, options: { theme: string; size: string; width: number }) => void;
        };
      };
    };
  }
}

interface GoogleLoginButtonProps {
  onCredential: (idToken: string) => void;
}

export function GoogleLoginButton({ onCredential }: GoogleLoginButtonProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLDivElement>(null);
  const [scriptError, setScriptError] = useState('');
  const [width, setWidth] = useState(0);

  // Measure parent width using ResizeObserver to ensure exact calculated width
  useEffect(() => {
    if (!containerRef.current) return;

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const parentW = entry.target.parentElement?.offsetWidth || entry.contentRect.width;
        if (parentW > 0) {
          const clamped = Math.max(200, Math.min(320, parentW));
          setWidth(clamped);
        }
      }
    });

    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!env.googleClientId || !buttonRef.current || width === 0) {
      return;
    }

    const render = () => {
      if (!window.google || !buttonRef.current) {
        return;
      }

      window.google.accounts.id.initialize({
        client_id: env.googleClientId,
        callback: response => onCredential(response.credential)
      });

      buttonRef.current.innerHTML = '';
      window.google.accounts.id.renderButton(buttonRef.current, {
        theme: 'outline',
        size: 'large',
        width: width
      });
    };

    if (window.google) {
      render();
      return;
    }

    const existingScript = document.querySelector<HTMLScriptElement>('script[src="https://accounts.google.com/gsi/client"]');
    if (existingScript) {
      existingScript.addEventListener('load', render, { once: true });
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = render;
    script.onerror = () => setScriptError('Không tải được Google Sign-In. Kiểm tra internet, ad blocker hoặc OAuth origin.');
    document.head.appendChild(script);
  }, [onCredential, width]);

  if (!env.googleClientId) {
    return <p className="auth-note">Thiếu VITE_GOOGLE_CLIENT_ID trong file .env.</p>;
  }

  return (
    <>
      <div className="auth-google-button" ref={containerRef} style={{ width: '100%', display: 'flex', justifyContent: 'center' }}>
        {width > 0 && <div key={width} ref={buttonRef} style={{ width: '100%', display: 'flex', justifyContent: 'center' }} />}
      </div>
      {scriptError && <p className="auth-note">{scriptError}</p>}
    </>
  );
}
