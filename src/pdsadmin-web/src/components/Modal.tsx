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
      <div className="fixed inset-0 bg-black/40" onClick={onClose} />
      <form
        onSubmit={handleSubmit}
        className="relative bg-white rounded-lg shadow-xl border border-gray-200 w-full max-w-sm mx-4 p-6"
      >
        <h2 className="text-lg font-semibold mb-4">{title}</h2>
        <label className="block text-sm text-gray-500 mb-1">{label}</label>
        <input
          ref={inputRef}
          type={inputType}
          defaultValue={initialValue ?? ''}
          placeholder={placeholder}
          className="w-full px-3 py-2 rounded bg-white border border-gray-300 text-gray-900 focus:border-blue-500 focus:outline-none mb-5"
        />
        <div className="flex gap-3 justify-end">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 bg-gray-100 text-gray-700 border border-gray-300 rounded text-sm hover:bg-gray-200"
          >
            Cancel
          </button>
          <button
            type="submit"
            className="px-4 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
          >
            Confirm
          </button>
        </div>
      </form>
    </div>
  );
}
