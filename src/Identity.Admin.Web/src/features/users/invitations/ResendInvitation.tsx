import { useState } from 'react';
import { App, Button } from 'antd';
import { ConfirmAction } from '@/components/ConfirmAction';
import { useAuth } from '@/features/auth/authContext';
import { InvitationError } from './InvitationError';
import { useInvitationCommand } from './useInvitationCommand';

function ResendAction({ userId }: { userId: string }) {
  const [open, setOpen] = useState(false);
  const command = useInvitationCommand();
  const { message } = App.useApp();
  async function confirm() {
    try {
      if (await command.execute({ kind: 'resend', userId })) {
        setOpen(false);
        void message.success('Invitation accepted for processing.');
      }
    } catch {
      /* Error stays in the confirmation. Never automatically repeat a POST. */
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
        Resend invitation
      </Button>
      <ConfirmAction
        open={open}
        title="Resend invitation?"
        confirmText="Request another invitation"
        description="Request a new invitation email for this account. Previous invitation links will stop working. Email delivery is not guaranteed."
        pending={command.pending}
        onConfirm={() => void confirm()}
        onCancel={() => setOpen(false)}
      >
        <InvitationError error={command.error} />
      </ConfirmAction>
    </>
  );
}

export function ResendInvitation({
  userId,
  status,
}: {
  userId: string;
  status: string;
}) {
  const { canWrite } = useAuth();
  return canWrite && status === 'Invited' ? (
    <ResendAction key={userId} userId={userId} />
  ) : null;
}
