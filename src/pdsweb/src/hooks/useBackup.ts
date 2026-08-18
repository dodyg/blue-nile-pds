import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApiGet, adminApiPost, downloadAdminFile } from '../api/adminClient';
import { backupKeys } from '../api/queryKeys';
import type { BackupCreateResponse, BackupListResponse, BackupStatus } from '../types/admin';

export function useBackupStatus() {
  return useQuery({
    queryKey: backupKeys.status,
    queryFn: () => adminApiGet<BackupStatus>('backup/status'),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'running' ? 2000 : false;
    },
  });
}

export function useBackupList() {
  return useQuery({
    queryKey: backupKeys.list,
    queryFn: () => adminApiGet<BackupListResponse>('backup/list'),
  });
}

export function useCreateBackup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => adminApiPost<BackupCreateResponse>('backup/create'),
    onSuccess: () => {
      queryClient.setQueryData(backupKeys.status, { status: 'running' } as BackupStatus);
      queryClient.invalidateQueries({ queryKey: backupKeys.all });
    },
  });
}

export function useDeleteBackup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (fileName: string) => adminApiPost('backup/delete', { fileName }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: backupKeys.list });
    },
  });
}

export function useDownloadBackup() {
  return useMutation({
    mutationFn: (fileName: string) => downloadAdminFile('backup/download', { fileName }),
  });
}
