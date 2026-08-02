import { useEffect, useRef } from 'react';

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
        className="relative bg-surface rounded-md shadow-modal border border-subtle w-full max-w-sm mx-4 p-6"
      >
        <h2 className="text-lg font-semibold mb-4">{title}</h2>
        <label className="block text-sm text-secondary mb-1">{label}</label>
        <input
          ref={inputRef}
          type={inputType}
          defaultValue={initialValue ?? ''}
          placeholder={placeholder}
          className="w-full px-3 py-2 rounded-md bg-surface border border-input text-ink focus:border-focus-ring focus:outline-none mb-5"
        />
        <div className="flex gap-3 justify-end">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-hover text-ghost border border-input rounded-md text-sm hover:bg-subtle focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Cancel
          </button>
          <button
            type="submit"
            className="px-4 py-2 bg-primary text-surface rounded-md text-sm hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            Confirm
          </button>
        </div>
      </form>
    </div>
  );
}
