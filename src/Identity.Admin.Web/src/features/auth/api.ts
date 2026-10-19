import { apiClient } from '@/api/httpClient';
import { ApiError, ClientError, isRecord } from '@/api/errors';
import type { components } from '@/api/schema';

export type AdminProfile = components['schemas']['AdminProfile'];
export type LoginInput = components['schemas']['AdminLoginRequest'];

function profile(value: unknown): AdminProfile {
  if (
    !isRecord(value) ||
    typeof value.id !== 'string' ||
    typeof value.email !== 'string' ||
    !(value.displayName === null || typeof value.displayName === 'string') ||
    typeof value.role !== 'string'
  )
    throw new ClientError('invalid_response');
  if (!['IdentityAdministrator', 'IdentityReadOnly'].includes(value.role))
    throw new ApiError(403, { code: 'forbidden' });
  // Only the documented, safe profile fields may enter the query cache.
  return {
    id: value.id,
    email: value.email,
    displayName: value.displayName,
    role: value.role,
  };
}

export async function getSession(signal: AbortSignal) {
  try {
    return profile(
      await apiClient.request('get', '/api/v1/admin/auth/me', { signal }),
    );
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null;
    throw error;
  }
}

export async function login(input: LoginInput, signal: AbortSignal) {
  await apiClient.request('post', '/api/v1/admin/auth/login', {
    body: input,
    signal,
  });
  // Confirm the cookie and obtain capabilities from /me before exposing the shell.
  const current = await getSession(signal);
  if (!current) throw new ApiError(401, { code: 'unauthorized' });
  return current;
}

export async function logout(signal: AbortSignal) {
  await apiClient.request('post', '/api/v1/admin/auth/logout', { signal });
}
