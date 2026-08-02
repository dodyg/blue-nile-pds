import type { HTMLAttributes, ReactNode } from 'react';

const base = 'rounded-md border border-subtle bg-surface shadow-card';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
}

function Card({ className = '', children, ...rest }: CardProps) {
  return (
    <div className={`${base} ${className}`} {...rest}>
      {children}
    </div>
  );
}

export { Card };
export default Card;

interface CardHeaderProps {
  title: string;
  subtitle?: ReactNode;
  actions?: ReactNode;
  className?: string;
}

export function CardHeader({ title, subtitle, actions, className = '' }: CardHeaderProps) {
  return (
    <div className={`flex items-start justify-between gap-3 border-b border-subtle px-4 py-3 ${className}`}>
      <div>
        <h2 className="font-display text-sm font-bold tracking-[0.08em] text-ink">{title}</h2>
        {subtitle && <p className="mt-0.5 text-xs text-muted">{subtitle}</p>}
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}
