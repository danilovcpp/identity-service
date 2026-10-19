// Route state only; never accept an absolute URL, fragment or arbitrary query string.
// Feature filters can add explicit query allowlists when those routes are implemented.
export function safeReturnPath(value: unknown): string {
  return typeof value === 'string' &&
    /^\/admin(?:\/[A-Za-z0-9_-]+)*\/?$/.test(value) &&
    !/^\/admin\/login(?:\/|$)/i.test(value)
    ? value
    : '/admin';
}
