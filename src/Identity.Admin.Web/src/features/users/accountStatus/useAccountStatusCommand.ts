import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ClientError } from '@/api/errors';
import { queryKeys } from '@/api/queryKeys';
import { changeAccountStatus, type StatusCommand } from './api';

export function useAccountStatusCommand() {
  const client = useQueryClient();
  const submitting = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const mutation = useMutation({
    mutationFn: (command: StatusCommand) => {
      const operation = controller.current;
      if (!operation) throw new ClientError('aborted');
      return changeAccountStatus(
        command,
        AbortSignal.any([operation.signal, AbortSignal.timeout(30_000)]),
      );
    },
    onSuccess: () => {
      // users.all covers lists, details and every cached session page, including
      // inactive queries. The server, never an optimistic update, supplies status.
      void client.invalidateQueries({ queryKey: queryKeys.users.all });
      void client.invalidateQueries({ queryKey: queryKeys.dashboard() });
      void client.invalidateQueries({ queryKey: queryKeys.audit.all });
    },
  });
  return {
    pending: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
    async execute(command: StatusCommand) {
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
