import { useEffect, useRef } from 'react';
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

  useEffect(() => {
    if (!env.googleClientId || !buttonRef.current) {
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

      window.google.accounts.id.renderButton(buttonRef.current, {
        theme: 'outline',
        size: 'large',
        width: 320
      });
    };

    if (window.google) {
      render();
      return;
    }

    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = render;
    document.head.appendChild(script);
  }, [onCredential]);

  if (!env.googleClientId) {
    return <p className="auth-note">Thiếu VITE_GOOGLE_CLIENT_ID trong file .env.</p>;
  }

  return <div ref={buttonRef} />;
}
