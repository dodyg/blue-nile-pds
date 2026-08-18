import type { ReactNode } from 'react';

interface BoardStatProps {
  label: string;
  value: ReactNode;
  sub?: ReactNode;
  className?: string;
}

export default function BoardStat({ label, value, sub, className = '' }: BoardStatProps) {
  return (
    <div className={`rounded-md border border-black/15 bg-board p-3 shadow-chip ${className}`}>
      <div className="text-[10px] font-mono font-semibold uppercase tracking-[0.18em] text-board-text-dim">{label}</div>
      <div key={String(value)} className="mt-1 font-display text-2xl font-bold tabular-nums text-board-text animate-flap">
        {value}
      </div>
      {sub && <div className="mt-0.5 text-xs text-board-text-dim">{sub}</div>}
    </div>
  );
}
