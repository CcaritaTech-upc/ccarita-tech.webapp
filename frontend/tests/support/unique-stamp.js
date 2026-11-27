import crypto from 'node:crypto';

// Client emails reject four or more identical characters in a row (422), which a
// timestamp plus random digits can produce. Every test email stamp goes through
// here so that rule never makes a run fail at random.
export function withoutLongRuns(value) {
  return String(value).replace(/(.)\1{3,}/g, '$1$1');
}

export function uniqueStamp() {
  return withoutLongRuns(`${Date.now()}${crypto.randomInt(100, 1000)}`);
}
