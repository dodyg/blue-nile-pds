import type { ReactNode } from 'react';

interface EmptyStateProps {
  title: string;
  description?: ReactNode;
  action?: ReactNode;
  className?: string;
}

export default function EmptyState({ title, description, action, className = '' }: EmptyStateProps) {
  return (
    <div className={`flex flex-col items-center justify-center rounded-md border border-dashed border-subtle bg-surface px-6 py-12 text-center ${className}`}>
      <div className="font-display text-lg font-bold tracking-tight text-muted">◌</div>
      <p className="mt-2 font-display text-sm font-semibold tracking-[0.08em] text-ghost">{title}</p>
      {description && <p className="mt-1 max-w-sm text-sm text-muted">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}
