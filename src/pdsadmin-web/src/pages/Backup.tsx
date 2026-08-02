import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import ConfirmDialog from '../components/ConfirmDialog';
import Button from '../components/Button';
import Badge from '../components/Badge';
import PageHeader from '../components/PageHeader';
import { TableBoard, Table, Th, Tr, Td } from '../components/Table';
import EmptyState from '../components/EmptyState';
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
      <PageHeader
        eyebrow="operations · archive"
        title="Backups"
        description="Snapshots of the account stores, kept in the backup directory."
        actions={
          <Button variant="primary" onClick={handleCreate} disabled={isBusy}>
            {isBusy ? 'Backing up...' : 'Create backup'}
          </Button>
        }
      />

      {message && <p className="mb-4 text-sm text-success-deep dark:text-success">{message}</p>}
      {(statusQuery.error || listQuery.error) && (
        <p className="mb-4 text-sm text-danger">{(statusQuery.error || listQuery.error)?.message}</p>
      )}

      {status === 'running' && (
        <div className="mb-4 flex items-center gap-3 rounded-md border border-subtle bg-surface p-5 shadow-card">
          <span className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" aria-hidden="true" />
          <span className="font-mono text-xs uppercase tracking-[0.12em] text-neutral">Backup in progress · page refreshes automatically</span>
        </div>
      )}

      {status === 'failed' && (
        <div className="mb-4 flex items-center gap-2 rounded-md border border-danger/40 bg-danger/10 p-5">
          <Badge tone="danger">failed</Badge>
          <span className="text-sm text-danger-deep dark:text-danger">{statusQuery.data?.error ?? 'Unknown error'}</span>
        </div>
      )}

      {backups.length === 0 && !statusQuery.isPending ? (
        <EmptyState
          title="No backups yet"
          description="Create your first backup to snapshot the account stores."
          action={<Button variant="primary" onClick={handleCreate} disabled={isBusy}>{isBusy ? 'Backing up...' : 'Create backup'}</Button>}
        />
      ) : (
        <TableBoard>
          <Table>
            <thead>
              <Tr>
                <Th>File</Th>
                <Th>Created</Th>
                <Th>Size</Th>
                <Th />
              </Tr>
            </thead>
            <tbody>
              {backups.map(b => (
                <Tr key={b.fileName}>
                  <Td className="font-mono text-xs">{b.fileName}</Td>
                  <Td className="text-xs">{new Date(b.createdAt).toLocaleString()}</Td>
                  <Td className="text-xs">{formatBytes(b.sizeBytes)}</Td>
                  <Td className="whitespace-nowrap text-right">
                    <Button variant="ghost" size="sm" onClick={() => handleDownload(b.fileName)} disabled={downloadMutation.isPending}>
                      Download
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      className="ml-2 text-danger hover:text-danger-deep"
                      onClick={() => setConfirm({ fileName: b.fileName })}
                      disabled={deleteMutation.isPending}
                    >
                      Delete
                    </Button>
                  </Td>
                </Tr>
              ))}
            </tbody>
          </Table>
        </TableBoard>
      )}

      {confirm && (
        <ConfirmDialog
          open
          title="Delete backup"
          message={`Delete ${confirm.fileName}? This cannot be undone.`}
          confirmLabel="Delete"
          confirmClass="bg-danger text-white dark:bg-danger-deep hover:opacity-90"
          onConfirm={handleDelete}
          onCancel={() => setConfirm(null)}
        />
      )}
    </div>
  );
}
