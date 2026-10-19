import { createContext, useContext } from 'react';
import type { AdminProfile, LoginInput } from './api';
export type Status =
  'loading' | 'authenticated' | 'unauthenticated' | 'forbidden' | 'error';
type Auth = {
  status: Status;
  profile: AdminProfile | null;
  canWrite: boolean;
  error: unknown;
  pending: boolean;
  signIn: (input: LoginInput) => Promise<void>;
  signOut: () => Promise<void>;
  retry: () => void;
};
export const AuthContext = createContext<Auth | null>(null);

export function useAuth() {
  const auth = useContext(AuthContext);
  if (!auth) throw new Error('AuthProvider is required.');
  return auth;
}
