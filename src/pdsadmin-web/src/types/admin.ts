export interface Account {
  did: string;
  handle: string;
  email?: string;
}

export interface GetAccountInfosResponse {
  accounts: Account[];
}

export interface GetAccountInfoResponse {
  did: string;
  handle: string;
  email?: string;
  emailConfirmedAt?: string;
  indexedAt?: string;
  inviteNote?: string;
  invitesDisabled?: boolean;
  takedownRef?: string;
  deactivatedAt?: string;
  createdAt?: string;
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
  uses: { usedBy: string; usedAt: string }[];
}

export interface GetInviteCodesResponse {
  codes: InviteCode[];
}

export interface SubjectStatus {
  subject: { did?: string; uri?: string };
  takedown?: { applied: boolean };
  deactivated?: { applied: boolean };
}

export interface GetSubjectStatusResponse {
  subjectStatus: SubjectStatus | SubjectStatus[];
}

export interface SearchAccountsResponse {
  accounts: { did: string; handle: string; email?: string }[];
  cursor?: string;
}
