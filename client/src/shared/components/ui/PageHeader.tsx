import React from 'react';
import './PageHeader.css';

export interface PageHeaderProps {
  icon: React.ReactNode;
  eyebrow: React.ReactNode;
  title: string;
  description: React.ReactNode;
  rightContent?: React.ReactNode;
  className?: string;
  onBack?: () => void;
}

export function PageHeader({ icon, eyebrow, title, description, rightContent, className, onBack }: PageHeaderProps) {
  return (
    <section className={`page-header-band ${className || ''}`}>
      <div className="page-header-left-area">
        {onBack && (
          <button 
            type="button" 
            className="page-header-back-btn" 
            onClick={onBack}
            title="Quay lại"
          >
            <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
              <line x1="19" y1="12" x2="5" y2="12"/><polyline points="12 19 5 12 12 5"/>
            </svg>
          </button>
        )}
        <div className="page-header-icon-box">
          {icon}
        </div>
        <div className="page-header-text">
          <p className="page-header-eyebrow">{eyebrow}</p>
          <h2 className="page-header-title">{title}</h2>
          <div className="page-header-description">{description}</div>
        </div>
      </div>
      {rightContent && (
        <div className="page-header-right-area">
          {rightContent}
        </div>
      )}
    </section>
  );
}
