import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { uploadBlob, xrpcGet, xrpcPost } from '../api/client';
import { accountKeys } from '../api/queryKeys';
import { clearAccessJwt, setAccessJwt } from '../stores/userAuth';
import type {
  BlobRef,
  ConfirmEmailRequest,
  CreateAccountRequest,
  CreateSessionRequest,
  GetRecordResponse,
  GetSessionResponse,
  ProfileRecord,
  RequestEmailUpdateResponse,
  RequestPasswordResetRequest,
  ResetPasswordRequest,
  SessionResponse,
  SetAccountProfileRequest,
  SetAccountProfileResponse,
  UpdateEmailRequest,
} from '../types/pds';

export function useSession(enabled = true) {
  return useQuery({
    queryKey: accountKeys.session,
    queryFn: () => xrpcGet<GetSessionResponse>('com.atproto.server.getSession', {}),
    retry: 1,
    enabled,
  });
}

export function useProfile(did: string | undefined) {
  return useQuery({
    queryKey: accountKeys.profile,
    queryFn: async (): Promise<
      { record: ProfileRecord; cid?: string } | null
    > => {
      try {
        const res = await xrpcGet<GetRecordResponse>('com.atproto.repo.getRecord', {
          repo: did!,
          collection: 'app.bsky.actor.profile',
          rkey: 'self',
        });
        return { record: res.value as ProfileRecord, cid: res.cid };
      } catch {
        return null;
      }
    },
    enabled: !!did,
  });
}

export function useCreateAccount() {
  return useMutation({
    mutationFn: (body: CreateAccountRequest) =>
      xrpcPost<SessionResponse>('com.atproto.server.createAccount', body),
    onSuccess: (session) => {
      if (session.accessJwt) setAccessJwt(session.accessJwt);
    },
  });
}

export function useSetAccountProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SetAccountProfileRequest) =>
      xrpcPost<SetAccountProfileResponse>('africa.bsky.setAccountProfile', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.session });
    },
  });
}

export function useCreateSession() {
  return useMutation({
    mutationFn: (body: CreateSessionRequest) =>
      xrpcPost<SessionResponse>('com.atproto.server.createSession', body),
    onSuccess: (session) => setAccessJwt(session.accessJwt),
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (vars: {
      did: string;
      record: ProfileRecord;
      swapRecord?: string;
    }) => {
      const body: Record<string, unknown> = {
        repo: vars.did,
        collection: 'app.bsky.actor.profile',
        rkey: 'self',
        record: vars.record,
      };
      if (vars.swapRecord) body.swapRecord = vars.swapRecord;
      return xrpcPost('com.atproto.repo.putRecord', body);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.profile });
    },
  });
}

export function useUploadAvatar() {
  return useMutation({
    mutationFn: ({ blob, mimeType }: { blob: Blob; mimeType: string }) =>
      uploadBlob(blob, mimeType),
  });
}

export function useLogout() {
  const queryClient = useQueryClient();
  return () => {
    clearAccessJwt();
    queryClient.clear();
  };
}

export function toBlobRef(upload: { blob: BlobRef }): BlobRef {
  return upload.blob;
}

export function useRequestEmailConfirmation() {
  return useMutation({
    mutationFn: () => xrpcPost('com.atproto.server.requestEmailConfirmation', {}),
  });
}

export function useConfirmEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ConfirmEmailRequest) =>
      xrpcPost('com.atproto.server.confirmEmail', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.session });
    },
  });
}

export function useRequestEmailUpdate() {
  return useMutation({
    mutationFn: () =>
      xrpcPost<RequestEmailUpdateResponse>('com.atproto.server.requestEmailUpdate', {}),
  });
}

export function useUpdateEmail() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateEmailRequest) =>
      xrpcPost('com.atproto.server.updateEmail', body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: accountKeys.session });
    },
  });
}

export function useRequestPasswordReset() {
  return useMutation({
    mutationFn: (body: RequestPasswordResetRequest) =>
      xrpcPost('com.atproto.server.requestPasswordReset', body),
  });
}

export function useResetPassword() {
  return useMutation({
    mutationFn: (body: ResetPasswordRequest) =>
      xrpcPost('com.atproto.server.resetPassword', body),
  });
}