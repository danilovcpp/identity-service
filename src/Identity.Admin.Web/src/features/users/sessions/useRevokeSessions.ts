import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ClientError } from '@/api/errors';
import { queryKeys } from '@/api/queryKeys';
import { revokeSessions, type RevokeCommand } from './api';

export function useRevokeSessions() {
  const client = useQueryClient();
  const submitting = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const mutation = useMutation({
    mutationFn: (command: RevokeCommand) => {
      const operation = controller.current;
      if (!operation) throw new ClientError('aborted');
      return revokeSessions(
        command,
        AbortSignal.any([operation.signal, AbortSignal.timeout(30_000)]),
      );
    },
    onSuccess: (_, command) => {
      // The detail prefix includes every cached session page for this user.
      void client.invalidateQueries({
        queryKey: queryKeys.users.detail(command.userId),
      });
      void client.invalidateQueries({ queryKey: queryKeys.dashboard() });
      void client.invalidateQueries({ queryKey: queryKeys.audit.all });
    },
  });
  return {
    pending: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
    async execute(command: RevokeCommand) {
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
