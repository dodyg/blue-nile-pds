import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import ConfirmDialog from '../components/ConfirmDialog';
import { backupKeys } from '../api/queryKeys';
import { useBackupList, useBackupStatus, useCreateBackup, useDeleteBackup, useDownloadBackup } from '../hooks/useBackup';

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${units[i]}`;
}

export default function Backup() {
  const [message, setMessage] = useState('');
  const [confirm, setConfirm] = useState<{ fileName: string } | null>(null);
  const queryClient = useQueryClient();
  const prevStatus = useRef<string | undefined>(undefined);

  const statusQuery = useBackupStatus();
  const listQuery = useBackupList();
  const createMutation = useCreateBackup();
  const deleteMutation = useDeleteBackup();
  const downloadMutation = useDownloadBackup();

  const status = statusQuery.data?.status;
  const backups = listQuery.data?.backups ?? [];
  const isBusy = status === 'running' || createMutation.isPending;

  useEffect(() => {
    if (status === 'completed') {
      queryClient.invalidateQueries({ queryKey: backupKeys.list });
      if (prevStatus.current === 'running') {
        setMessage(`Backup ready: ${statusQuery.data?.fileName ?? 'unknown'}`);
      }
    }
    prevStatus.current = status;
  }, [status, statusQuery.data, queryClient]);

  function handleCreate() {
    setMessage('');
    createMutation.mutate(undefined, {
      onSuccess: () => setMessage('Backup started'),
      onError: (e: Error) => setMessage(e.message),
    });
  }

  function handleDownload(fileName: string) {
    downloadMutation.mutate(fileName, {
      onError: (e: Error) => setMessage(e.message),
    });
  }

  function handleDelete() {
    if (!confirm) return;
    const fileName = confirm.fileName;
    setConfirm(null);
    deleteMutation.mutate(fileName, {
      onSuccess: () => setMessage(`Deleted ${fileName}`),
      onError: (e: Error) => setMessage(e.message),
    });
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-bold">Backups</h1>
        <button
          onClick={handleCreate}
          disabled={isBusy}
          className="px-4 py-2 bg-primary text-surface rounded-md text-sm font-medium hover:bg-primary-hover disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
        >
          {isBusy ? 'Backing up...' : 'Create backup'}
        </button>
      </div>

      {message && <p className="text-success mb-4">{message}</p>}
      {(statusQuery.error || listQuery.error) && (
        <p className="text-danger mb-4">{(statusQuery.error || listQuery.error)?.message}</p>
      )}

      {status === 'running' && (
        <div className="bg-surface border border-subtle shadow-card rounded-md p-5 mb-4 flex items-center gap-3">
          <span className="w-4 h-4 border-2 border-primary border-t-transparent rounded-full animate-spin" aria-hidden="true" />
          <span className="text-neutral">A backup is in progress... This page refreshes automatically.</span>
        </div>
      )}

      {status === 'failed' && (
        <div className="bg-danger border border-subtle shadow-card rounded-md p-5 mb-4 text-surface">
          <span className="font-medium">Last backup failed: </span>
          {statusQuery.data?.error ?? 'Unknown error'}
        </div>
      )}

      <div className="bg-surface border border-subtle rounded-md overflow-hidden overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-subtle text-left text-secondary">
              <th className="p-3 font-medium">File</th>
              <th className="p-3 font-medium">Created</th>
              <th className="p-3 font-medium">Size</th>
              <th className="p-3 font-medium" />
            </tr>
          </thead>
          <tbody>
            {backups.map(b => (
              <tr key={b.fileName} className="border-b border-subtle">
                <td className="p-3 font-mono text-xs">{b.fileName}</td>
                <td className="p-3 text-xs">{new Date(b.createdAt).toLocaleString()}</td>
                <td className="p-3 text-xs">{formatBytes(b.sizeBytes)}</td>
                <td className="p-3 whitespace-nowrap text-right">
                  <button
                    onClick={() => handleDownload(b.fileName)}
                    disabled={downloadMutation.isPending}
                    className="text-primary hover:text-primary-hover text-xs font-medium disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                  >
                    Download
                  </button>
                  <button
                    onClick={() => setConfirm({ fileName: b.fileName })}
                    disabled={deleteMutation.isPending}
                    className="ml-3 text-danger hover:text-danger-hover text-xs font-medium disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-focus-ring"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
            {backups.length === 0 && (
              <tr>
                <td colSpan={4} className="p-6 text-center text-muted">No backups yet</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {confirm && (
        <ConfirmDialog
          open
          title="Delete backup"
          message={`Delete ${confirm.fileName}? This cannot be undone.`}
          confirmLabel="Delete"
          confirmClass="bg-danger hover:bg-danger-hover"
          onConfirm={handleDelete}
          onCancel={() => setConfirm(null)}
        />
      )}
    </div>
  );
}
