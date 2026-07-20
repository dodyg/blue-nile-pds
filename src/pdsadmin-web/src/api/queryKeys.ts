export const accountKeys = {
  all: ['accounts'] as const,
  search: (email?: string) => [...accountKeys.all, 'search', email] as const,
  detail: (did: string) => [...accountKeys.all, 'detail', did] as const,
  subjectStatus: (did: string) => [...accountKeys.all, 'subjectStatus', did] as const,
};

export const inviteKeys = {
  all: ['invites'] as const,
  codes: (cursor?: string) => [...inviteKeys.all, 'codes', cursor] as const,
};

export const dashboardKeys = {
  stats: ['dashboard', 'stats'] as const,
};
