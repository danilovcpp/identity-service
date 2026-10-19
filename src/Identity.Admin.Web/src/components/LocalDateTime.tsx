const formatter = new Intl.DateTimeFormat('en-US', {
  dateStyle: 'medium',
  timeStyle: 'short',
});

export function LocalDateTime({ value }: { value: string }) {
  const date = new Date(value);
  return (
    <time dateTime={value} title={date.toISOString()}>
      {formatter.format(date)}
    </time>
  );
}
