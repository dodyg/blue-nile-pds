import type { ReactNode } from 'react';

type Tone = 'neutral' | 'success' | 'warning' | 'danger' | 'accent' | 'board';

const tones: Record<Tone, string> = {
  neutral: 'bg-hover text-secondary border border-subtle',
  success: 'bg-success/15 text-success-deep dark:text-success',
  warning: 'bg-warning-bg text-warning-text',
  danger: 'bg-danger/15 text-danger-deep dark:text-danger',
  accent: 'bg-accent-soft text-primary dark:text-board-text',
  board: 'bg-board text-board-text shadow-chip',
};

const base =
  'inline-flex items-center gap-1 rounded-sm px-2 py-0.5 font-mono text-[10px] font-semibold uppercase tracking-[0.12em] whitespace-nowrap';

interface BadgeProps {
  tone?: Tone;
  children: ReactNode;
  className?: string;
}

export default function Badge({ tone = 'neutral', className = '', children }: BadgeProps) {
  return <span className={`${base} ${tones[tone]} ${className}`}>{children}</span>;
}
