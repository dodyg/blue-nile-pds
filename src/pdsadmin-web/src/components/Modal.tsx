import { useEffect, useRef } from 'react';
import Button from './Button';
import { Input } from './Input';

interface ModalProps {
  open: boolean;
  title: string;
  label: string;
  initialValue?: string;
  placeholder?: string;
  inputType?: 'text' | 'password';
  onConfirm: (value: string) => void;
  onClose: () => void;
}

export default function Modal({ open, title, label, initialValue, placeholder, inputType = 'text', onConfirm, onClose }: ModalProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (open) {
      const id = setTimeout(() => inputRef.current?.focus(), 0);
      return () => clearTimeout(id);
    }
  }, [open]);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const val = inputRef.current?.value.trim();
    if (val) onConfirm(val);
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="fixed inset-0 bg-overlay" onClick={onClose} />
      <form
        onSubmit={handleSubmit}
        className="relative w-full max-w-sm rounded-md border border-subtle bg-surface p-6 shadow-modal"
      >
        <h2 className="font-display text-base font-bold tracking-[0.08em] text-ink">{title}</h2>
        <label className="mb-1.5 mt-4 block font-mono text-[10px] font-semibold uppercase tracking-[0.16em] text-secondary">{label}</label>
        <Input
          ref={inputRef}
          type={inputType}
          defaultValue={initialValue ?? ''}
          placeholder={placeholder}
        />
        <div className="mt-5 flex justify-end gap-3">
          <Button variant="secondary" onClick={onClose}>Cancel</Button>
          <Button variant="primary" type="submit">Confirm</Button>
        </div>
      </form>
    </div>
  );
}
