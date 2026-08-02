interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  confirmClass?: string;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  confirmClass = 'bg-danger hover:bg-danger-hover',
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="fixed inset-0 bg-overlay" onClick={onCancel} />
      <div className="relative bg-surface rounded-md shadow-modal border border-subtle w-full max-w-sm mx-4 p-6">
        <h2 className="text-lg font-semibold mb-2">{title}</h2>
        <p className="text-neutral text-sm mb-5">{message}</p>
        <div className="flex gap-3 justify-end">
          <button
            type="button"
            onClick={onCancel}
            className="px-4 py-2 bg-hover text-ghost border border-input rounded-md text-sm hover:bg-subtle focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className={`px-4 py-2 text-surface rounded-md text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring ${confirmClass}`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
