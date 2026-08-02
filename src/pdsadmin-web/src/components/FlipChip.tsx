import type { ReactNode } from 'react';

const tones = {
  board: 'bg-board text-board-text shadow-chip',
  accent: 'bg-accent-soft text-primary dark:text-board-text',
  surface: 'bg-surface text-ghost border border-input',
} as const;

export type FlipChipTone = keyof typeof tones;

interface FlipChipProps {
  tone?: FlipChipTone;
  children: ReactNode;
  className?: string;
  title?: string;
}

export default function FlipChip({ tone = 'board', children, className = '', title }: FlipChipProps) {
  return (
    <span
      title={title}
      className={`inline-flex items-center gap-1.5 rounded-sm border border-black/15 px-2.5 py-1 font-mono text-xs whitespace-nowrap select-none ${tones[tone]} ${className}`}
    >
      {children}
    </span>
  );
}
