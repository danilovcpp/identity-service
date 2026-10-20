export const passwordHint =
  'Use 15–128 Unicode characters after NFKC normalization. Spaces are allowed. Avoid common passwords.';

export function passwordProblem(value: string): string | undefined {
  if (!value) return 'Enter a new password.';
  // TextEncoder replaces lone surrogates; the backend rejects them instead.
  if (/\p{Surrogate}/u.test(value)) return 'Enter valid Unicode characters.';
  const normalized = value.normalize('NFKC');
  const size = Array.from(normalized).length;
  if (size < 15 || size > 128)
    return 'Use 15–128 Unicode characters after normalization.';
  const encoder = new TextEncoder();
  if (
    encoder.encode(value).length > 1024 ||
    encoder.encode(normalized).length > 512
  )
    return 'This password exceeds the supported input size.';
  return undefined;
}
