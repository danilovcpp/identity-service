export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export type ClientErrorKind =
  'network' | 'aborted' | 'timeout' | 'invalid_response' | 'invalid_request';

const clientMessages: Record<ClientErrorKind, string> = {
  network: 'Unable to reach Identity. Check your connection.',
  aborted: 'The request was cancelled.',
  timeout: 'The request timed out.',
  invalid_response: 'Identity returned an unexpected response.',
  invalid_request: 'The request is invalid.',
};

export class ClientError extends Error {
  readonly kind: ClientErrorKind;

  constructor(kind: ClientErrorKind) {
    super(clientMessages[kind]);
    this.name = 'ClientError';
    this.kind = kind;
  }
}

const httpMessages: Record<number, string> = {
  400: 'Check the submitted information.',
  401: 'Authentication is required or the credentials are invalid.',
  403: 'You do not have permission to perform this action.',
  404: 'The requested resource was not found.',
  409: 'The operation conflicts with the current state.',
  429: 'Too many requests. Try again later.',
};

// Retain only documented ProblemDetails extensions. Never keep the raw response,
// request, URL, server detail/instance, or the original fetch exception as a cause.
export class ApiError extends Error {
  readonly kind = 'http';
  readonly code: string;
  readonly status: number;
  readonly traceId?: string;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;

  constructor(status: number, problem: unknown) {
    super(httpMessages[status] ?? 'Identity could not complete the request.');
    this.name = 'ApiError';
    this.status = status;
    const data = isRecord(problem) ? problem : {};
    this.code =
      typeof data.code === 'string' && /^[a-z][a-z0-9_]{0,79}$/.test(data.code)
        ? data.code
        : `http_${status.toString()}`;
    this.traceId =
      typeof data.traceId === 'string' &&
      /^[A-Za-z0-9_.:-]{1,128}$/.test(data.traceId)
        ? data.traceId
        : undefined;
    this.fieldErrors = Object.fromEntries(
      Object.entries(isRecord(data.errors) ? data.errors : {}).flatMap(
        ([field, messages]) =>
          Array.isArray(messages) &&
          messages.every((value) => typeof value === 'string')
            ? [[field, messages]]
            : [],
      ),
    );
  }
}

export function cancellationError(signal: AbortSignal): ClientError {
  return new ClientError(
    isRecord(signal.reason) && signal.reason.name === 'TimeoutError'
      ? 'timeout'
      : 'aborted',
  );
}
