import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { pendingGet, pendingPost, pendingPut, publicPendingGet, publicPendingPost } from '../api/pendingClient';
import { pendingKeys } from '../api/queryKeys';
import { clearPendingTokens, setPendingTokens } from '../stores/pendingAuth';

export interface PendingConfigResponse {
  approvalRequired: boolean;
}

export interface PendingProfileResponse {
  email: string;
  handle: string;
  location?: string;
  accountType?: string;
  displayName?: string;
  description?: string;
  status: string;
  emailConfirmed?: boolean;
  emailConfirmedAt?: string;
  createdAt: string;
}

export interface PendingRegisterResponse {
  accessJwt: string;
  refreshJwt: string;
}

export interface PendingLoginResponse {
  accessJwt: string;
  refreshJwt: string;
  status: string;
}

export function usePendingConfig() {
  return useQuery({
    queryKey: pendingKeys.config,
    queryFn: () => publicPendingGet<PendingConfigResponse>('config'),
    staleTime: 60_000,
  });
}

export function usePendingProfile() {
  return useQuery({
    queryKey: pendingKeys.profile,
    queryFn: () => pendingGet<PendingProfileResponse>('profile'),
    retry: 1,
  });
}

export function usePendingRegister() {
  return useMutation({
    mutationFn: (body: { email: string; handle: string; password: string; inviteCode?: string; location?: string; accountType?: string }) =>
      publicPendingPost<PendingRegisterResponse>('register', body),
    onSuccess: (data) => {
      setPendingTokens(data.accessJwt, data.refreshJwt);
    },
  });
}

export function usePendingLogin() {
  return useMutation({
    mutationFn: (body: { identifier: string; password: string }) =>
      publicPendingPost<PendingLoginResponse>('login', body),
    onSuccess: (data) => {
      setPendingTokens(data.accessJwt, data.refreshJwt);
    },
  });
}

export function usePendingUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { displayName?: string; description?: string; location?: string; accountType?: string }) =>
      pendingPut<void>('profile', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pendingKeys.profile });
    },
  });
}

export function usePendingValidateInvite() {
  return useMutation({
    mutationFn: (code: string) =>
      publicPendingGet<{ valid: boolean; error?: string }>(`validate-invite/${encodeURIComponent(code)}`),
  });
}

export function usePendingRequestEmailConfirmation() {
  return useMutation({
    mutationFn: () => pendingPost<void>('requestEmailConfirmation', {}),
  });
}

export function usePendingConfirmEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { token: string }) => pendingPost<void>('confirmEmail', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pendingKeys.profile });
    },
  });
}

export function usePendingRequestEmailUpdate() {
  return useMutation({
    mutationFn: () => pendingPost<{ tokenRequired: boolean }>('requestEmailUpdate', {}),
  });
}

export function usePendingUpdateEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { email: string; token?: string }) => pendingPost<void>('updateEmail', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: pendingKeys.profile });
    },
  });
}

export function usePendingLogout() {
  const queryClient = useQueryClient();
  return () => {
    clearPendingTokens();
    queryClient.removeQueries({ queryKey: pendingKeys.profile });
  };
}
