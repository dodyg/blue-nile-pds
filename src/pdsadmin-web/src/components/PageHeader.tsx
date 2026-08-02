import type { ReactNode } from 'react';

interface PageHeaderProps {
  eyebrow?: string;
  title: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  className?: string;
}

export default function PageHeader({ eyebrow, title, description, actions, className = '' }: PageHeaderProps) {
  return (
    <header className={`mb-6 ${className}`}>
      {eyebrow && (
        <p className="mb-1 font-display text-[10px] font-semibold uppercase tracking-[0.24em] text-muted">{eyebrow}</p>
      )}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h1 className="font-display text-2xl font-bold tracking-tight text-ink">{title}</h1>
          {description && <p className="mt-1 max-w-prose text-sm text-secondary">{description}</p>}
        </div>
        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </header>
  );
}
