import type { ReactNode, SyntheticEvent } from 'react';

interface AuthLayoutProps {
  title: string;
  subtitle: string;
  children: ReactNode;
  compact?: boolean;
}

export function AuthLayout({ title, subtitle, children, compact = false }: AuthLayoutProps) {
  const handleHeroFallback = (event: SyntheticEvent<HTMLImageElement>) => {
    event.currentTarget.src = '/images/auth-rental-hero.png';
  };

  return (
    <main className={`auth-page ${compact ? 'auth-page-compact' : ''}`}>
      <section className="auth-shell" aria-label={title}>
        <aside className="auth-visual" aria-hidden="true">
          <img
            className="auth-hero-image"
            src="/images/auth-hero-user.png"
            alt=""
            onError={handleHeroFallback}
          />
        </aside>

        <section className="auth-panel">
          <div className="auth-panel-brand">
            <span className="auth-panel-logo" aria-hidden="true" />
            <span>Smart Rental Platform</span>
          </div>
          <h1>{title}</h1>
          <p className="auth-subtitle">{subtitle}</p>
          {children}
        </section>
      </section>
    </main>
  );
}
