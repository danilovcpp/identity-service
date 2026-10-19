import { useEffect, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ClientError } from '@/api/errors';
import { queryKeys } from '@/api/queryKeys';
import { requestPasswordReset } from './api';

export function usePasswordResetCommand() {
  const client = useQueryClient();
  const submitting = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const mutation = useMutation({
    mutationFn: (userId: string) => {
      const operation = controller.current;
      if (!operation) throw new ClientError('aborted');
      return requestPasswordReset(
        userId,
        AbortSignal.any([operation.signal, AbortSignal.timeout(30_000)]),
      );
    },
    onSuccess: () => {
      // Requesting a link does not change the account or its sessions.
      void client.invalidateQueries({ queryKey: queryKeys.audit.all });
    },
  });
  return {
    pending: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
    async execute(userId: string) {
      if (submitting.current) return false;
      submitting.current = true;
      controller.current = new AbortController();
      try {
        await mutation.mutateAsync(userId);
        return true;
      } finally {
        submitting.current = false;
        controller.current = null;
      }
    },
  };
}
