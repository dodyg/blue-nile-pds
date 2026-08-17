import { useQuery } from '@tanstack/react-query';
import { xrpcGet } from '../api/client';
import { serverKeys } from '../api/queryKeys';
import type { CheckHandleAvailabilityResponse, DescribeServerResponse } from '../types/pds';

export function useDescribeServer() {
  return useQuery({
    queryKey: serverKeys.describe,
    queryFn: () =>
      xrpcGet<DescribeServerResponse>('com.atproto.server.describeServer', {}),
    staleTime: Infinity,
  });
}

export function useHandleAvailability(handle: string) {
  return useQuery({
    queryKey: ['register', 'availability', handle.trim().toLowerCase()],
    queryFn: () => xrpcGet<CheckHandleAvailabilityResponse>('com.atproto.temp.checkHandleAvailability', { handle }),
    enabled: handle.trim().length >= 3,
    staleTime: 10_000,
  });
}