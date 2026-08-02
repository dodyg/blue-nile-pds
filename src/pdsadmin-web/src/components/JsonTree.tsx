import { useState, type ReactNode } from 'react';

function valueColor(value: unknown): string {
  if (value === null) return 'text-data-null';
  if (typeof value === 'string') return 'text-data-string';
  if (typeof value === 'number') return 'text-data-number';
  if (typeof value === 'boolean') return 'text-data-boolean';
  return '';
}

function formatPrimitive(value: unknown): string {
  if (value === null) return 'null';
  if (typeof value === 'string') return `"${value}"`;
  return String(value);
}

function isExpandable(value: unknown): value is Record<string, unknown> | unknown[] {
  return value !== null && typeof value === 'object';
}

function isImageBlob(value: unknown, did?: string): string | null {
  if (!did || !value || typeof value !== 'object') return null;
  const obj = value as Record<string, unknown>;
  if (obj.$type === 'blob' && typeof (obj.ref as Record<string, unknown>)?.['$link'] === 'string' &&
      typeof obj.mimeType === 'string' && (obj.mimeType as string).startsWith('image/')) {
    const cid = (obj.ref as Record<string, unknown>)['$link'] as string;
    return `/xrpc/com.atproto.sync.getBlob?did=${encodeURIComponent(did)}&cid=${encodeURIComponent(cid)}`;
  }
  return null;
}

function JsonNode({ label, value, depth, did }: { label?: string; value: unknown; depth: number; did?: string }) {
  const [expanded, setExpanded] = useState(depth < 2);

  if (!isExpandable(value)) {
    return (
      <div className="flex items-start gap-1.5">
        {label !== undefined && <span className="text-neutral shrink-0">{label}:</span>}
        <span className={`${valueColor(value)} break-all`}>{formatPrimitive(value)}</span>
      </div>
    );
  }

  const imgSrc = isImageBlob(value, did);

  const entries = Array.isArray(value)
    ? value.map((v, i) => [String(i), v] as const)
    : Object.entries(value);

  const isEmpty = entries.length === 0;

  return (
    <div>
      <button
        onClick={() => setExpanded(!expanded)}
        className="flex items-center gap-1 text-neutral hover:text-ink cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
      >
        <span className="text-xs w-3 shrink-0">{expanded ? '▼' : '▶'}</span>
        {label !== undefined && <span className="text-neutral">{label}:</span>}
        <span className="text-muted text-xs">
          {Array.isArray(value) ? `[${value.length}]` : `{${entries.length}}`}
        </span>
      </button>
      {expanded && !isEmpty && (
        <div className="ml-4 pl-2 border-l border-subtle space-y-0.5">
          {entries.map(([k, v]) => (
            <JsonNode key={k} label={k} value={v} depth={depth + 1} did={did} />
          ))}
          {imgSrc && (
            <img src={imgSrc} alt="blob" className="mt-1 max-h-64 rounded border border-subtle" onError={e => { (e.target as HTMLImageElement).style.display = 'none'; }} />
          )}
        </div>
      )}
      {expanded && isEmpty && (
        <div className="ml-4 text-muted text-xs">{Array.isArray(value) ? '[]' : '{}'}</div>
      )}
    </div>
  );
}

interface Props {
  value: unknown;
  className?: string;
  did?: string;
}

export default function JsonTree({ value, className = '', did }: Props): ReactNode {
  return <div className={`text-xs font-mono ${className}`}><JsonNode value={value} depth={0} did={did} /></div>;
}
