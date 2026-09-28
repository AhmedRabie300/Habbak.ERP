/**
 * `todayLocal()` — the pattern used almost everywhere in this codebase
 * to default a date field to "today" — is subtly wrong: `toISOString()` converts to UTC first,
 * so for any user in a positive-UTC-offset timezone (Egypt is UTC+2/+3), during the first few
 * hours after local midnight the UTC instant is still "yesterday", and the expression silently
 * returns yesterday's date. Reproduces with:
 *   TZ="Africa/Cairo" node -e "console.log(new Date('2026-09-11T00:30:00+03:00').toISOString().slice(0,10))"
 *   // prints 2026-09-10 — wrong
 * These two helpers format from the Date's own local components instead of going through UTC.
 */
export function toLocalDateString(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function todayLocal(): string {
  return toLocalDateString(new Date());
}
