import type { ButtonHTMLAttributes, ReactNode } from 'react';
import './Card.css';

export type CardStatusTone =
  | 'success'
  | 'warning'
  | 'danger'
  | 'neutral'
  | 'info'
  | 'reserved';

export type CardActionVariant =
  | 'primary'
  | 'secondary'
  | 'danger'
  | 'success'
  | 'outline';

export interface CardAction extends ButtonHTMLAttributes<HTMLButtonElement> {
  label: ReactNode;
  icon?: ReactNode;
  variant?: CardActionVariant;
}

interface CardProps {
  title: ReactNode;
  children: ReactNode;
  status?: ReactNode;
  statusTone?: CardStatusTone;
  statusClassName?: string;
  actionItems?: CardAction[];
  actions?: ReactNode;
  className?: string;
  headerClassName?: string;
  bodyClassName?: string;
  actionsClassName?: string;
  tone?: CardStatusTone;
  bodyColumns?: 1 | 2;
}

interface CardMetaRowProps {
  label: ReactNode;
  value: ReactNode;
  icon?: ReactNode;
  className?: string;
  valueClassName?: string;
}

export function Card({
  title,
  children,
  status,
  statusTone = 'neutral',
  statusClassName = '',
  actionItems,
  actions,
  className = '',
  headerClassName = '',
  bodyClassName = '',
  actionsClassName = '',
  tone = statusTone,
  bodyColumns = 1,
}: CardProps) {
  const hasActions = Boolean(actions) || Boolean(actionItems?.length);

  return (
    <article className={`ui-card ui-card--${tone} ${className}`.trim()}>
      <header className={`ui-card__header ${headerClassName}`.trim()}>
        <h3 className="ui-card__title">{title}</h3>
        {status && (
          <span className={`ui-card__status ui-card__status--${statusTone} ${statusClassName}`.trim()}>
            {status}
          </span>
        )}
      </header>

      <div className={`ui-card__body ui-card__body--columns-${bodyColumns} ${bodyClassName}`.trim()}>
        {children}
      </div>

      {hasActions && (
        <footer className={`ui-card__actions ${actionsClassName}`.trim()}>
          {actionItems?.map(({ label, icon, variant = 'primary', className = '', type = 'button', ...buttonProps }, index) => (
            <button
              key={index}
              type={type}
              className={`ui-card-action ui-card-action--${variant} ${className}`.trim()}
              {...buttonProps}
            >
              {icon && <span className="ui-card-action__icon">{icon}</span>}
              <span>{label}</span>
            </button>
          ))}
          {actions}
        </footer>
      )}
    </article>
  );
}

export function CardMetaRow({
  label,
  value,
  icon,
  className = '',
  valueClassName = '',
}: CardMetaRowProps) {
  return (
    <div className={`ui-card-meta-row ${className}`.trim()}>
      {icon && <span className="ui-card-meta-row__icon">{icon}</span>}
      <span className="ui-card-meta-row__label">{label}</span>
      <span className={`ui-card-meta-row__value ${valueClassName}`.trim()}>{value}</span>
    </div>
  );
}
