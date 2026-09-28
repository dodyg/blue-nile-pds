import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { adminApiGet, adminApiPost } from '../api/adminClient';
import { sequencerKeys } from '../api/queryKeys';
import type { SequencerAdvanceResponse, SequencerStatus } from '../types/admin';

export function useSequencerStatus() {
  return useQuery({
    queryKey: sequencerKeys.status,
    queryFn: () => adminApiGet<SequencerStatus>('sequencer/status'),
  });
}

export function useAdvanceSequencer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (targetSeq: number) =>
      adminApiPost<SequencerAdvanceResponse>('sequencer/advance', { targetSeq }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: sequencerKeys.all });
    },
  });
}
