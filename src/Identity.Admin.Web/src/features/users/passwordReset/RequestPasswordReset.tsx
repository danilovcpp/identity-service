import { useState } from 'react';
import { App, Button, Typography } from 'antd';
import { ApiError } from '@/api/errors';
import { CommandError } from '@/components/CommandError';
import { ConfirmAction } from '@/components/ConfirmAction';
import { useAuth } from '@/features/auth/authContext';
import { usePasswordResetCommand } from './usePasswordResetCommand';

type Props = {
  userId: string;
  email: string;
  status: string;
  emailVerified: boolean;
};

function ResetError({ error }: { error: unknown }) {
  if (!error) return null;
  const description =
    error instanceof ApiError && error.status === 409
      ? 'This account is no longer eligible for password reset. Refresh the account before trying again.'
      : error instanceof ApiError && error.code === 'password_reset_unavailable'
        ? 'Password reset is unavailable. Contact the Identity operator.'
        : undefined;
  return (
    <CommandError
      error={error}
      title="Unable to request password reset"
      description={description}
      recovery="Check security activity before submitting again."
    />
  );
}

function ResetAction({ userId, email }: Pick<Props, 'userId' | 'email'>) {
  const [open, setOpen] = useState(false);
  const command = usePasswordResetCommand();
  const { message } = App.useApp();
  async function confirm() {
    try {
      if (await command.execute(userId)) {
        setOpen(false);
        void message.success('Password reset request accepted for processing.');
      }
    } catch {
      // Keep the error in the confirmation. Never automatically repeat a POST.
    }
  }
  return (
    <>
      <Button
        onClick={() => {
          command.reset();
          setOpen(true);
        }}
      >
        Send password reset link
      </Button>
      <ConfirmAction
        open={open}
        title="Send password reset link?"
        confirmText="Request reset email"
        description={
          <>
            Request a password reset email for{' '}
            <Typography.Text strong>{email}</Typography.Text>. The recipient
            chooses their own password; administrators cannot see or set it.
            Previous reset links will stop working. Sessions are revoked when
            the recipient completes the reset. Email delivery is not guaranteed.
          </>
        }
        pending={command.pending}
        onConfirm={() => void confirm()}
        onCancel={() => setOpen(false)}
      >
        <ResetError error={command.error} />
      </ConfirmAction>
    </>
  );
}

export function RequestPasswordReset({
  userId,
  email,
  status,
  emailVerified,
}: Props) {
  const { canWrite } = useAuth();
  // Credentials and current eligibility are checked by the server, never inferred.
  return canWrite && status === 'Active' && emailVerified ? (
    <ResetAction key={userId} userId={userId} email={email} />
  ) : null;
}
