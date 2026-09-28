// Pure rules for the device control panel: what can be manipulated given the
// device's committed power state.

// Returns true (on), false (off), or null when the power state is unknown.
export function parsePower(value) {
  if (value === true) return true;
  if (value === false) return false;
  const text = String(value ?? '').toLowerCase();
  if (text === 'true' || text === 'on') return true;
  if (text === 'false' || text === 'off') return false;
  return null;
}

// A powered-off device only accepts the `power` command; every other attribute
// is locked. An unknown power state locks nothing.
export function isControlLocked(attributeName, powerState) {
  return attributeName !== 'power' && powerState === false;
}
