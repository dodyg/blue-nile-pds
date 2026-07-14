export interface InviteCodeUse {
  usedBy: string;
  usedAt: string;
}

export interface InviteCodeView {
  code: string;
  available: number;
  disabled: boolean;
  forAccount: string;
  createdBy: string;
  createdAt: string;
  uses: InviteCodeUse[];
}

export interface GetAccountInfoResponse {
  did: string;
  handle: string;
  email?: string;
  emailConfirmedAt?: string;
  invitesDisabled?: boolean;
  indexedAt: string;
  deactivatedAt?: string;
  invitedBy?: InviteCodeView;
  invites?: InviteCodeView[];
  relatedRecords?: unknown[];
  inviteNote?: string;
  threatSignatures?: { property: string; value: string }[];
}

export interface SearchAccountsResponse {
  accounts: GetAccountInfoResponse[];
  cursor?: string;
}

export interface RepoEntry {
  did: string;
}

export interface ListReposResponse {
  repos: RepoEntry[];
}

export interface InviteCode {
  code: string;
  available: number;
  disabled: boolean;
  forAccount?: string;
  createdBy?: string;
  createdAt?: string;
  uses: { usedBy: string; usedAt: string }[];
}

export interface GetInviteCodesResponse {
  codes: InviteCode[];
  cursor?: string;
}

export interface SubjectStatus {
  subject: { did?: string; uri?: string };
  takedown?: { applied: boolean; ref?: string };
  deactivated?: { applied: boolean };
}
