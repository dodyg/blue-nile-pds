export const directoryKeys = {
  page: (cursor?: string) => ['directory', 'page', cursor] as const,
  lookup: (search: string) => ['directory', 'lookup', search] as const,
};

export const serverKeys = {
  describe: ['server', 'describe'] as const,
};

export const accountKeys = {
  session: ['account', 'session'] as const,
  profile: ['account', 'profile'] as const,
};