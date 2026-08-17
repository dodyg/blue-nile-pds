import type { ReactNode } from 'react';

interface Props {
  loading?: boolean;
  error?: boolean;
  children: ReactNode;
}

export default function AsyncState({ loading, error, children }: Props) {
  if (loading) {
    return <p className="py-6 text-center text-xs uppercase tracking-[0.16em] text-muted">Loading…</p>;
  }
  if (error) {
    return <p className="py-6 text-center text-sm text-danger">Something went wrong</p>;
  }
  return <>{children}</>;
}