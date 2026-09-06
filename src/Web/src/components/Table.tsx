import type { ReactNode } from 'react';

export function TableBoard({ className = '', children }: { className?: string; children: ReactNode }) {
  return (
    <div className={`overflow-x-auto rounded-md border border-subtle bg-surface shadow-card ${className}`}>
      {children}
    </div>
  );
}

export function Table({ className = '', children }: { className?: string; children: ReactNode }) {
  return <table className={`w-full border-collapse text-sm ${className}`}>{children}</table>;
}

export function Th({ className = '', children }: { className?: string; children?: ReactNode }) {
  return (
    <th className={`border-b border-subtle bg-surface-raised p-2 text-left font-display text-[10px] font-semibold uppercase tracking-[0.16em] text-muted whitespace-nowrap ${className}`}>
      {children}
    </th>
  );
}

export function Tr({ className = '', children }: { className?: string; children?: ReactNode }) {
  return <tr className={`border-b border-subtle transition-colors last:border-0 hover:bg-row-hover ${className}`}>{children}</tr>;
}

export function Td({ className = '', children, title, colSpan }: { className?: string; children?: ReactNode; title?: string; colSpan?: number }) {
  return (
    <td colSpan={colSpan} title={title} className={`p-2 align-top ${className}`}>
      {children}
    </td>
  );
}
