import type { ReactNode } from 'react';

interface AlertProps {
  type?: 'success' | 'error' | 'info';
  children: ReactNode;
}

export function Alert({ type = 'info', children }: AlertProps) {
  return (
    <div className={`alert alert-${type}`} role="alert">
      {children}
    </div>
  );
}
