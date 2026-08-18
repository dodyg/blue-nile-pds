export const directoryKeys = {
  page: (cursor?: string) => ['directory', 'page', cursor] as const,
  lookup: (search: string) => ['directory', 'lookup', search] as const,
};

export const serverKeys = {
  describe: ['server', 'describe'] as const,
};

export const accountKeys = {
  all: ['accounts'] as const,
  search: (email?: string) => [...accountKeys.all, 'search', email] as const,
  detail: (did: string) => [...accountKeys.all, 'detail', did] as const,
  subjectStatus: (did: string) => [...accountKeys.all, 'subjectStatus', did] as const,
  session: ['account', 'session'] as const,
  profile: ['account', 'profile'] as const,
};

export const inviteKeys = {
  all: ['invites'] as const,
  codes: (cursor?: string) => [...inviteKeys.all, 'codes', cursor] as const,
};

export const dashboardKeys = {
  stats: ['dashboard', 'stats'] as const,
};

export const backupKeys = {
  all: ['backups'] as const,
  status: ['backups', 'status'] as const,
  list: ['backups', 'list'] as const,
};

export const repoResyncKeys = {
  all: ['repoResync'] as const,
  status: ['repoResync', 'status'] as const,
};
