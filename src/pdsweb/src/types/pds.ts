export interface ListReposEntry {
  did: string;
  head: string;
  rev: string;
  active: boolean;
  status: string;
}

export interface ListReposResponse {
  repos: ListReposEntry[];
  cursor?: string;
}

export interface DescribeRepoResponse {
  handle: string;
  did: string;
  didDoc: Record<string, unknown>;
  collections: string[];
  handleIsCorrect: boolean;
}

export interface GetRecordResponse {
  uri: string;
  cid: string;
  value: unknown;
}

export interface BlobRef {
  $type: 'blob';
  ref: { $link: string };
  mimeType: string;
  size: number;
}

export interface ProfileRecord {
  $type: 'app.bsky.actor.profile';
  displayName?: string;
  description?: string;
  avatar?: BlobRef;
  createdAt: string;
}

export interface Person {
  did: string;
  handle: string;
  displayName?: string;
  description?: string;
  avatarCid?: string;
}

export interface DescribeServerResponse {
  inviteCodeRequired: boolean;
  availableUserDomains: string[];
}

export interface CheckHandleAvailabilitySuggestion {
  handle: string;
  method: string;
}

export interface CheckHandleAvailabilityResultAvailable {
  $type: 'com.atproto.temp.checkHandleAvailability#resultAvailable';
}

export interface CheckHandleAvailabilityResultUnavailable {
  $type: 'com.atproto.temp.checkHandleAvailability#resultUnavailable';
  suggestions: CheckHandleAvailabilitySuggestion[];
}

export type CheckHandleAvailabilityResult =
  | CheckHandleAvailabilityResultAvailable
  | CheckHandleAvailabilityResultUnavailable;

export interface CheckHandleAvailabilityResponse {
  handle: string;
  result: CheckHandleAvailabilityResult;
}

export interface CreateAccountRequest {
  email: string;
  handle: string;
  password: string;
  inviteCode?: string;
}

export interface CreateSessionRequest {
  identifier: string;
  password: string;
}

export interface SessionResponse {
  accessJwt: string;
  refreshJwt: string;
  handle: string;
  did: string;
  email?: string;
}

export interface GetSessionResponse {
  did: string;
  handle: string;
  email?: string;
  emailConfirmed?: boolean;
}

export interface RequestEmailUpdateResponse {
  tokenRequired: boolean;
}

export interface ConfirmEmailRequest {
  email: string;
  token: string;
}

export interface UpdateEmailRequest {
  email: string;
  emailAuthFactor?: boolean;
  token?: string;
}

export interface RequestPasswordResetRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  password: string;
}