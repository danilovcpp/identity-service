import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ApiError, ClientError } from '@/api/errors';
import { CommandError } from './CommandError';

describe('command error policy', () => {
  it.each([
    new ClientError('network'),
    new ClientError('timeout'),
    new ClientError('invalid_response'),
    new ApiError(500, { detail: 'private diagnostic' }),
  ])(
    'treats transport/server uncertainty as an unconfirmed write (%#)',
    (error) => {
      render(
        <CommandError
          error={error}
          title="Unable to complete"
          recovery="Refresh before submitting again."
        />,
      );
      expect(screen.getByRole('alert')).toHaveTextContent(
        'The result could not be confirmed.',
      );
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Refresh before submitting again.',
      );
      expect(screen.queryByText('private diagnostic')).not.toBeInTheDocument();
    },
  );
  it('shares safe CSRF feedback and lets features supply specific conflicts', () => {
    const { rerender } = render(
      <CommandError
        error={new ApiError(400, { code: 'csrf_invalid' })}
        title="Unable to complete"
        recovery="Refresh."
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Your security check expired.',
    );
    rerender(
      <CommandError
        error={new ApiError(409, {})}
        title="Unable to complete"
        description="The account changed."
        recovery="Refresh."
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('The account changed.');
  });
});
