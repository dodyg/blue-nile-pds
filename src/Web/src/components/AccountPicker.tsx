import { useEffect, useId, useRef, useState } from 'react';
import { useAccountAutocomplete, useAccountInfo } from '../hooks/useAccounts';
import { Input } from './Input';
import Badge from './Badge';
import type { GetAccountInfoResponse } from '../types/admin';

export interface PickedAccount {
  did: string;
  handle: string;
  email?: string;
  invitesDisabled?: boolean;
}

function toPicked(a: GetAccountInfoResponse): PickedAccount {
  return { did: a.did, handle: a.handle, email: a.email, invitesDisabled: a.invitesDisabled };
}

function useDebouncedValue(value: string, delayMs: number) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(t);
  }, [value, delayMs]);
  return debounced;
}

interface AccountPickerProps {
  value: PickedAccount[];
  onChange: (next: PickedAccount[]) => void;
  multi?: boolean;
  disabled?: boolean;
  autoFocus?: boolean;
}

export default function AccountPicker({ value, onChange, multi = false, disabled = false, autoFocus = false }: AccountPickerProps) {
  const [text, setText] = useState('');
  const [open, setOpen] = useState(false);
  const [highlight, setHighlight] = useState(0);
  const debounced = useDebouncedValue(text, 300);
  const { data, isFetching, error } = useAccountAutocomplete(debounced);
  const listId = useId();
  const boxRef = useRef<HTMLDivElement>(null);

  const trimmed = debounced.trim();
  const isDidInput = trimmed.startsWith('did:');
  const { data: didLookup, isFetching: didFetching } = useAccountInfo(isDidInput ? trimmed : '');

  const results = (data?.accounts ?? []).filter(a => !value.some(v => v.did === a.did));

  useEffect(() => {
    setHighlight(0);
  }, [debounced, data?.accounts.length]);

  useEffect(() => {
    if (text) setOpen(true);
  }, [text]);

  function select(picked: PickedAccount) {
    if (multi) {
      if (!value.some(v => v.did === picked.did)) onChange([...value, picked]);
    } else {
      onChange([picked]);
    }
    setText('');
    setOpen(false);
  }

  function remove(did: string) {
    onChange(value.filter(v => v.did !== did));
  }

  const showDidRow = isDidInput && trimmed.length > 0;
  const totalRows = results.length + (showDidRow ? 1 : 0);

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Escape') {
      setOpen(false);
      return;
    }
    if (!open || totalRows === 0) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setHighlight(h => (h + 1) % totalRows);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setHighlight(h => (h - 1 + totalRows) % totalRows);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (highlight < results.length) {
        select(toPicked(results[highlight]));
      } else if (showDidRow && didLookup) {
        select(toPicked(didLookup));
      }
    }
  }

  return (
    <div ref={boxRef} className="relative">
      {multi && value.length > 0 && (
        <div className="mb-2 flex flex-wrap gap-1.5">
          {value.map(v => (
            <span
              key={v.did}
              className="inline-flex items-center gap-1.5 rounded-sm border border-subtle bg-hover px-2 py-1 text-xs"
            >
              <span className="font-medium text-ink">{v.handle}</span>
              <span className="max-w-[140px] truncate font-mono text-[10px] text-muted">{v.did}</span>
              {v.invitesDisabled && <Badge tone="warning">invites off</Badge>}
              <button
                type="button"
                aria-label={`Remove ${v.handle}`}
                onClick={() => remove(v.did)}
                disabled={disabled}
                className="ml-1 rounded-sm px-1 text-muted hover:text-danger focus-visible:outline-2 focus-visible:outline-focus-ring"
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}

      {!multi && value.length > 0 && (
        <div className="mb-2 flex items-center gap-2 rounded-sm border border-subtle bg-hover px-3 py-2 text-sm">
          <div className="min-w-0 flex-1">
            <div className="font-medium text-ink">{value[0].handle}</div>
            <div className="truncate font-mono text-xs text-muted">{value[0].did}</div>
            {value[0].email && <div className="truncate text-xs text-secondary">{value[0].email}</div>}
          </div>
          {value[0].invitesDisabled && <Badge tone="warning">invites off</Badge>}
          <button
            type="button"
            aria-label="Clear selection"
            onClick={() => onChange([])}
            disabled={disabled}
            className="rounded-sm px-2 py-1 text-muted hover:text-danger focus-visible:outline-2 focus-visible:outline-focus-ring"
          >
            ×
          </button>
        </div>
      )}

      <Input
        type="text"
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-activedescendant={open && totalRows > 0 ? `${listId}-opt-${highlight}` : undefined}
        aria-autocomplete="list"
        placeholder="Search by handle or email..."
        value={text}
        autoFocus={autoFocus}
        disabled={disabled}
        onChange={e => setText(e.target.value)}
        onFocus={() => { if (text) setOpen(true); }}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onKeyDown={handleKeyDown}
      />

      {open && text.trim() && (
        <div
          id={listId}
          role="listbox"
          className="absolute z-20 mt-1 max-h-64 w-full overflow-auto rounded-sm border border-subtle bg-surface shadow-card"
        >
          {isFetching && <p className="px-3 py-2 text-xs text-secondary">Searching...</p>}
          {error && <p className="px-3 py-2 text-xs text-danger">{error.message}</p>}
          {!isFetching && !error && results.length === 0 && !showDidRow && (
            <p className="px-3 py-2 text-xs text-secondary">No accounts found. Try a different search, or paste a DID via “Paste DID”.</p>
          )}
          {results.map((a, i) => (
            <button
              key={a.did}
              id={`${listId}-opt-${i}`}
              type="button"
              role="option"
              aria-selected={i === highlight}
              onMouseDown={e => { e.preventDefault(); select(toPicked(a)); }}
              onMouseEnter={() => setHighlight(i)}
              className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm ${i === highlight ? 'bg-hover' : ''}`}
            >
              <span className="min-w-0 flex-1">
                <span className="block truncate font-medium text-ink">{a.handle}</span>
                <span className="block truncate text-xs text-secondary">{a.email || a.did}</span>
              </span>
              {a.invitesDisabled && <Badge tone="warning">invites off</Badge>}
            </button>
          ))}
          {showDidRow && (
            <div className="border-t border-subtle">
              {didFetching ? (
                <p className="px-3 py-2 text-xs text-secondary">Looking up {trimmed}...</p>
              ) : didLookup ? (
                <button
                  type="button"
                  role="option"
                  aria-selected={highlight === results.length}
                  onMouseDown={e => { e.preventDefault(); select(toPicked(didLookup)); }}
                  onMouseEnter={() => setHighlight(results.length)}
                  className={`flex w-full items-center gap-2 px-3 py-2 text-left text-sm ${highlight === results.length ? 'bg-hover' : ''}`}
                >
                  <span className="min-w-0 flex-1">
                    <span className="block truncate font-medium text-ink">Use {didLookup.handle} directly</span>
                    <span className="block truncate font-mono text-xs text-secondary">{didLookup.did}</span>
                  </span>
                  {didLookup.invitesDisabled && <Badge tone="warning">invites off</Badge>}
                </button>
              ) : (
                <p className="px-3 py-2 text-xs text-secondary">No account found for {trimmed}.</p>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
