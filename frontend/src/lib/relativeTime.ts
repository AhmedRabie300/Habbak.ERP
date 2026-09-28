/** Docs/Implementation/Phase-2.5-Research.md §1.4 — Intl.RelativeTimeFormat (built into every
 * modern browser) instead of pulling in a date library just for "من 5 دقائق"/"5 minutes ago". */
/** The API serializes DateTime (Kind=Unspecified, since SQL Server's datetime2 carries no zone) as
 * a bare ISO string with no 'Z'/offset — `new Date(...)` would parse that as local time instead of
 * UTC, throwing every relative time off by the browser's own UTC offset (caught live: a
 * brand-new notification showed as "3 hours ago" on a UTC+3 machine). Every *AtUtc field from this
 * API needs this same treatment; scoped to this formatter for now since fixing the serialization
 * globally is outside Phase 2.5. */
function asUtcDate(isoDateUtc: string): Date {
  const hasZone = /Z$|[+-]\d{2}:\d{2}$/.test(isoDateUtc);
  return new Date(hasZone ? isoDateUtc : `${isoDateUtc}Z`);
}

export function formatRelativeTime(isoDateUtc: string, locale: string): string {
  const then = asUtcDate(isoDateUtc).getTime();
  const now = Date.now();
  const diffSeconds = Math.round((then - now) / 1000);

  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });

  const divisions: { amount: number; unit: Intl.RelativeTimeFormatUnit }[] = [
    { amount: 60, unit: 'seconds' },
    { amount: 60, unit: 'minutes' },
    { amount: 24, unit: 'hours' },
    { amount: 7, unit: 'days' },
    { amount: 4.34524, unit: 'weeks' },
    { amount: 12, unit: 'months' },
    { amount: Number.POSITIVE_INFINITY, unit: 'years' }
  ];

  let duration = diffSeconds;
  for (const division of divisions) {
    if (Math.abs(duration) < division.amount) {
      return rtf.format(Math.round(duration), division.unit);
    }
    duration /= division.amount;
  }
  return rtf.format(Math.round(duration), 'years');
}
