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
  const buttonRef = useRef<HTMLDivElement>(null);
  const renderedRef = useRef(false);
  const [scriptError, setScriptError] = useState('');

  useEffect(() => {
    if (!env.googleClientId || !buttonRef.current) {
      return;
    }

    const render = () => {
      if (!window.google || !buttonRef.current || renderedRef.current) {
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
        width: 320
      });
      renderedRef.current = true;
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
  }, [onCredential]);

  if (!env.googleClientId) {
    return <p className="auth-note">Thiếu VITE_GOOGLE_CLIENT_ID trong file .env.</p>;
  }

  return (
    <>
      <div ref={buttonRef} />
      {scriptError && <p className="auth-note">{scriptError}</p>}
    </>
  );
}
