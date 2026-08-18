const KEY = 'pds_admin_password';

export function getAdminPassword(): string | null {
  return sessionStorage.getItem(KEY);
}

export function setAdminPassword(password: string): void {
  sessionStorage.setItem(KEY, password);
}

export function clearAdminPassword(): void {
  sessionStorage.removeItem(KEY);
}

export function isAuthenticated(): boolean {
  return getAdminPassword() !== null;
}
