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

/** Validates company years in business, where a new company may have zero years (max 80). */
export function isValidYearsInBusiness(years, max = 80) {
  if (years === null || years === undefined || years === '') return false;
  const num = Number(years);
  return Number.isInteger(num) && num >= 0 && num <= max;
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
 * Comprehensive validator for real-estate project names.
 * Ensures the name has realistic length, contains letters/vowels,
 * and is not repetitive keystroke spam, symbols or injection strings.
 */
export function validateProjectName(name, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  if (!name || typeof name !== 'string' || !name.trim()) {
    return { isValid: false, error: tr('projects.validation.nameRequired', 'El nombre del proyecto es obligatorio.') };
  }
  const trimmed = name.trim();
  if (trimmed.length < 3) {
    return { isValid: false, error: tr('projects.validation.nameMinLength', 'El nombre del proyecto debe tener al menos 3 caracteres.') };
  }
  if (trimmed.length > 100) {
    return { isValid: false, error: tr('projects.validation.nameMaxLength', 'El nombre del proyecto no puede exceder los 100 caracteres.') };
  }
  if (/[<>]/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.nameNoHtml', 'El nombre no puede contener etiquetas ni caracteres HTML (<, >).') };
  }
  // Repetitive identical character spam (4+ consecutive identical chars)
  if (/(.)\1{3,}/i.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.nameRepetitive', 'El nombre no puede contener caracteres repetitivos continuos (ej. aaaa).') };
  }
  // Must contain at least 3 letters anywhere in the name
  const letters = trimmed.match(/[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]/g);
  if (!letters || letters.length < 3) {
    return { isValid: false, error: tr('projects.validation.nameInvalid', 'El nombre debe contener al menos 3 letras y no consistir únicamente en números o símbolos.') };
  }
  // Consonant spam check (if word is 4+ chars, must have at least one vowel)
  const vowels = trimmed.match(/[aeiouáéíóúAEIOUÁÉÍÓÚ]/i);
  if (trimmed.length >= 4 && !vowels) {
    return { isValid: false, error: tr('projects.validation.nameVowels', 'El nombre debe ser una palabra o término legible y contener al menos una vocal.') };
  }
  // Disallow forbidden special characters
  if (!/^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s.,\-#'&()/]+$/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.nameInvalid', 'El nombre contiene caracteres no permitidos.') };
  }
  return { isValid: true, error: null };
}

/**
 * Comprehensive validator for real-estate project locations.
 */
export function validateProjectLocation(location, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  if (!location || typeof location !== 'string' || !location.trim()) {
    return { isValid: false, error: tr('projects.validation.locationRequired', 'La ubicación del proyecto es obligatoria.') };
  }
  const trimmed = location.trim();
  if (trimmed.length < 3) {
    return { isValid: false, error: tr('projects.validation.locationMinLength', 'La ubicación debe tener al menos 3 caracteres.') };
  }
  if (trimmed.length > 150) {
    return { isValid: false, error: tr('projects.validation.locationMaxLength', 'La ubicación no puede exceder los 150 caracteres.') };
  }
  if (/[<>]/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.locationNoHtml', 'La ubicación no puede contener etiquetas ni caracteres HTML (<, >).') };
  }
  if (/(.)\1{3,}/i.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.locationRepetitive', 'La ubicación no puede contener caracteres repetitivos continuos.') };
  }
  const letters = trimmed.match(/[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]/g);
  if (!letters || letters.length < 3) {
    return { isValid: false, error: tr('projects.validation.locationInvalid', 'La ubicación debe contener al menos 3 letras y describir una dirección, calle o distrito válido.') };
  }
  if (!/^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s.,\-#'&()/°ºª]+$/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.locationInvalid', 'La ubicación contiene caracteres no permitidos.') };
  }
  return { isValid: true, error: null };
}

/**
 * Validator for project description.
 */
export function validateProjectDescription(description, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  if (!description || typeof description !== 'string' || !description.trim()) {
    return { isValid: true, error: null };
  }
  const trimmed = description.trim();
  if (trimmed.length > 500) {
    return { isValid: false, error: tr('projects.validation.descriptionMaxLength', 'La descripción no puede exceder los 500 caracteres.') };
  }
  if (/[<>]/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.descriptionNoHtml', 'La descripción no puede contener etiquetas ni caracteres HTML (<, >).') };
  }
  return { isValid: true, error: null };
}

/**
 * Validates a username.
 */
export function isValidUsername(username) {
  if (!username || typeof username !== 'string') return false;
  return USERNAME_REGEX.test(username.trim());
}

/**
 * Validates the account password minimum length.
 */
export function isValidPassword(password, minLength = 8) {
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
