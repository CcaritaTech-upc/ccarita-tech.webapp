/**
 * Centralized validation utility for IoBuild frontend forms.
 * Provides pure validator functions and composite validators for entities.
 */

// Email regex according to RFC 5322 standard
const EMAIL_REGEX = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

// Phone: optional '+' prefix, digits, spaces, hyphens, parentheses; 7 to 15 digits
const PHONE_REGEX = /^\+?[0-9\s\-()]{7,20}$/;

// Username: 3 to 30 characters, alphanumeric, underscores, hyphens, dots
const USERNAME_REGEX = /^[a-zA-Z0-9_.-]{3,30}$/;

// MAC address: 6 pairs of hex digits separated by colon or hyphen, or 12 continuous hex chars
const MAC_REGEX = /^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^[0-9A-Fa-f]{12}$/;

/**
 * Validates whether an email is well-formed.
 */
export function isValidEmail(email) {
  if (!email || typeof email !== 'string') return false;
  return EMAIL_REGEX.test(email.trim());
}

/**
 * Validates a telephone / mobile number (7 to 15 digits).
 */
export function isValidPhone(phone) {
  if (!phone || typeof phone !== 'string') return false;
  const trimmed = phone.trim();
  if (!PHONE_REGEX.test(trimmed)) return false;
  const digitsOnly = trimmed.replace(/\D/g, '');
  return digitsOnly.length >= 7 && digitsOnly.length <= 15;
}

/**
 * Validates an age (integer between min and max, defaults 18-120).
 */
export function isValidAge(age, min = 18, max = 120) {
  if (age === null || age === undefined || age === '') return false;
  const num = Number(age);
  return !isNaN(num) && Number.isInteger(num) && num >= min && num <= max;
}

/**
 * Validates a person's or entity's full name.
 */
export function isValidName(name, minLength = 2, maxLength = 100) {
  if (!name || typeof name !== 'string') return false;
  const trimmed = name.trim();
  return trimmed.length >= minLength && trimmed.length <= maxLength;
}

/**
 * Validates a username.
 */
export function isValidUsername(username) {
  if (!username || typeof username !== 'string') return false;
  return USERNAME_REGEX.test(username.trim());
}

/**
 * Validates password strength (min length).
 */
export function isValidPassword(password, minLength = 6) {
  if (!password || typeof password !== 'string') return false;
  return password.length >= minLength;
}

/**
 * Validates a MAC address (optional; if empty returns true).
 */
export function isValidMacAddress(mac) {
  if (!mac || typeof mac !== 'string' || !mac.trim()) return true;
  return MAC_REGEX.test(mac.trim());
}

/**
 * Validates positive integer in a given range.
 */
export function isValidPositiveInteger(val, min = 1, max = 100000) {
  if (val === null || val === undefined || val === '') return false;
  const num = Number(val);
  return !isNaN(num) && Number.isInteger(num) && num >= min && num <= max;
}

/**
 * Validates an optional URL.
 */
export function isValidUrl(url) {
  if (!url || typeof url !== 'string' || !url.trim()) return true;
  try {
    const parsed = new URL(url.trim());
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

/**
 * Cross-field: password confirmation must match (case-sensitive, no trim).
 * Empty confirmation never matches.
 */
export function doPasswordsMatch(password, confirmation) {
  if (typeof password !== 'string' || typeof confirmation !== 'string') return false;
  if (!confirmation) return false;
  return password === confirmation;
}

/**
 * Business rule: new password must differ from current password.
 * Returns false when either value is missing.
 */
export function isNewPasswordDifferent(currentPassword, newPassword) {
  if (typeof currentPassword !== 'string' || typeof newPassword !== 'string') return false;
  if (!currentPassword || !newPassword) return false;
  return currentPassword !== newPassword;
}

/**
 * Contextual eligibility policy (NOT a hardcoded age >= 18).
 * Business context decides the minimum age: product, jurisdiction, account type.
 */
export function isEligible({ dateOfBirth, product = 'default', jurisdiction = 'default', evaluationDate = new Date(), policies = null } = {}) {
  if (!dateOfBirth) return false;
  const dob = dateOfBirth instanceof Date ? dateOfBirth : new Date(dateOfBirth);
  const evalDate = evaluationDate instanceof Date ? evaluationDate : new Date(evaluationDate);
  if (Number.isNaN(dob.getTime()) || Number.isNaN(evalDate.getTime())) return false;
  if (dob > evalDate) return false;

  const defaultPolicies = {
    default: { default: 18 },
  };
  const table = policies ?? defaultPolicies;
  const minAge = table?.[product]?.[jurisdiction] ?? table?.[product]?.default ?? table?.default?.[jurisdiction] ?? 18;

  let age = evalDate.getFullYear() - dob.getFullYear();
  const monthDiff = evalDate.getMonth() - dob.getMonth();
  if (monthDiff < 0 || (monthDiff === 0 && evalDate.getDate() < dob.getDate())) age -= 1;
  return age >= minAge;
}
