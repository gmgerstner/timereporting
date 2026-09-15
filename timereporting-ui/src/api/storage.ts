import type { User } from '../models';

/** localStorage keys, kept identical to the Angular app so existing logins survive. */
export const StorageKeys = {
  rememberLogin: 'RememberLogin',
  username: 'Username',
  password: 'Password',
  token: 'Token',
  expires: 'Expires',
} as const;

export function getToken(): string {
  return localStorage.getItem(StorageKeys.token) ?? '';
}

export function getStoredUsername(): string {
  return localStorage.getItem(StorageKeys.username) ?? '';
}

export function setCredentials(user: User): void {
  localStorage.setItem(StorageKeys.username, user.username);
  localStorage.setItem(StorageKeys.token, user.token);
  localStorage.setItem(StorageKeys.expires, user.expires);
}

export function clearCredentials(): void {
  localStorage.setItem(StorageKeys.token, '');
  localStorage.setItem(StorageKeys.expires, '');
}

/** True when a token is stored and has not expired yet. */
export function hasValidSession(): boolean {
  const username = localStorage.getItem(StorageKeys.username);
  if (!username) {
    return false;
  }

  const expires = localStorage.getItem(StorageKeys.expires);
  if (!expires) {
    return false;
  }

  const expiresAt = new Date(expires);
  return !Number.isNaN(expiresAt.getTime()) && expiresAt > new Date();
}
