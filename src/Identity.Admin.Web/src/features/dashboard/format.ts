export const numberFormat = new Intl.NumberFormat('en-US');
const dayFormat = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  timeZone: 'UTC',
});
const fullDayFormat = new Intl.DateTimeFormat('en-US', {
  dateStyle: 'medium',
  timeZone: 'UTC',
});
const instantFormat = new Intl.DateTimeFormat('en-US', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

// API DateOnly values label UTC buckets; converting them to local dates would shift days.
export const formatDay = (date: string) =>
  dayFormat.format(new Date(`${date}T00:00:00Z`));
export const formatFullDay = (date: string) =>
  fullDayFormat.format(new Date(`${date}T00:00:00Z`));
export const formatInstant = (instant: string) =>
  instantFormat.format(new Date(instant));
