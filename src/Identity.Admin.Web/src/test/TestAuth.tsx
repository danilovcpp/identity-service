import type { PropsWithChildren } from 'react';
import { AuthContext } from '@/features/auth/authContext';
import { testProfile } from './auth';

export function TestAuth({
  children,
  canWrite = false,
}: PropsWithChildren<{ canWrite?: boolean }>) {
  return (
    <AuthContext.Provider
      value={{
        status: 'authenticated',
        canWrite,
        pending: false,
        error: null,
        profile: {
          ...testProfile,
          role: canWrite ? 'IdentityAdministrator' : 'IdentityReadOnly',
        },
        signIn: () => Promise.resolve(),
        signOut: () => Promise.resolve(),
        retry: () => undefined,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}
