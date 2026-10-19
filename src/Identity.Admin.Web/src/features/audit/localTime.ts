export function localDateInput(instant: string): string {
  if (!instant) return '';
  const date = new Date(instant);
  const pad = (value: number, width = 2) => String(value).padStart(width, '0');
  return `${pad(date.getFullYear(), 4)}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}`;
}
export function localInstant(value: string): string | undefined {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,3})?)?$/.test(value))
    return undefined;
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return undefined;
  // Reject calendar rollover and nonexistent local times at a DST transition.
  const normalized =
    value.length === 16
      ? value + ':00.000'
      : value.includes('.')
        ? value.padEnd(23, '0')
        : value + '.000';
  return localDateInput(date.toISOString()) === normalized &&
    date.getFullYear() >= 1
    ? date.toISOString()
    : undefined;
}
