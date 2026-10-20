import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/api/queryKeys';
import { ClientError } from '@/api/errors';
import { inviteUser, resendInvitation, type InviteInput } from './api';

type Command =
  { kind: 'invite'; input: InviteInput } | { kind: 'resend'; userId: string };

export function useInvitationCommand() {
  const client = useQueryClient();
  const submitting = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const mutation = useMutation({
    mutationFn: (command: Command) => {
      const operation = controller.current;
      if (!operation) throw new ClientError('aborted');
      const signal = AbortSignal.any([
        operation.signal,
        AbortSignal.timeout(30_000),
      ]);
      return command.kind === 'invite'
        ? inviteUser(command.input, signal)
        : resendInvitation(command.userId, signal);
    },
    onSuccess: (_, command) => {
      if (command.kind === 'invite') {
        void client.invalidateQueries({ queryKey: queryKeys.users.all });
        void client.invalidateQueries({ queryKey: queryKeys.dashboard() });
      } else {
        void client.invalidateQueries({
          queryKey: queryKeys.users.detail(command.userId),
        });
      }
      void client.invalidateQueries({ queryKey: queryKeys.audit.all });
    },
  });
  return {
    pending: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
    async execute(command: Command) {
      // The ref closes the gap before React renders the disabled submit button.
      if (submitting.current) return false;
      submitting.current = true;
      controller.current = new AbortController();
      try {
        await mutation.mutateAsync(command);
        return true;
      } finally {
        submitting.current = false;
        controller.current = null;
      }
    },
  };
}
