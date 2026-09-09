// Shared helpers for the play-night API. A leading underscore keeps this out of Vercel's
// automatic /api routing — a file directly under api/ becomes a route unless its name starts
// with `_`, so this one is importable without also being callable.

const MIN_OFFSET_MINUTES = -720; // UTC+12
const MAX_OFFSET_MINUTES = 840;  // UTC-14

/**
 * A play night's "session" is the host's own local calendar date — not the server's UTC one,
 * and not whatever a caller feels like sending.
 *
 * Two review findings on #103 both traced back to sessionKey() trusting the wrong thing:
 *   - the client sent a literal date string, so any caller could write into an arbitrary
 *     session, splitting real rollups or polluting the admin log;
 *   - deriving "today" from `new Date().toISOString()` uses UTC, which quietly split one
 *     evening's debriefs across two calendar dates for any US timezone (UTC midnight lands at
 *     4–8pm local, squarely inside a play night).
 *
 * The fix keeps the server's own clock as the only source of "now" — never a client-supplied
 * date — while still respecting the host's actual local midnight: the client sends its UTC
 * offset (`Date.prototype.getTimezoneOffset()`, minutes to ADD to local time to reach UTC), and
 * the calendar date is computed here from that.
 */
export function sessionDateFor(tzOffsetMinutes) {
  const offset = Number(tzOffsetMinutes);
  const safeOffset =
    Number.isFinite(offset) && offset >= MIN_OFFSET_MINUTES && offset <= MAX_OFFSET_MINUTES
      ? offset
      : 0; // an absent or out-of-range offset falls back to UTC rather than rejecting the call

  return new Date(Date.now() - safeOffset * 60000).toISOString().slice(0, 10);
}

export const SESSION_PATTERN = /^\d{4}-\d{2}-\d{2}$/;
