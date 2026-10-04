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
  return checkTextLegibility(trimmed, 'name', t);
}

/**
 * Helper to detect keyboard smashing, home row spam, consonant clusters, and unpronounceable text.
 */
function checkTextLegibility(text, fieldName, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  const trimmed = text.trim();
  const letters = trimmed.toLowerCase().match(/[a-záéíóúñü]/g) || [];
  const uniqueLetters = new Set(letters);
  const words = trimmed.split(/\s+/).filter(w => w.length > 0);

  // 1. Any individual word longer than 20 characters, or words without vowels
  for (const word of words) {
    const cleanWord = word.replace(/[^a-záéíóúñü]/gi, '');
    if (cleanWord.length > 20) {
      return { isValid: false, error: tr('projects.validation.wordTooLong', 'Contiene palabras excesivamente largas o no válidas.') };
    }
    if (cleanWord.length >= 3 && !/[aeiouáéíóúAEIOUÁÉÍÓÚyY]/i.test(cleanWord)) {
      return { isValid: false, error: tr('projects.validation.wordNoVowels', 'Cada palabra debe ser legible y contener vocales.') };
    }
  }

  // 2. Minimum words required for Description
  if (fieldName === 'description') {
    if (words.length < 2 || !trimmed.includes(' ')) {
      return { isValid: false, error: tr('projects.validation.descriptionWords', 'La descripción debe ser una frase u oración compuesta por varias palabras separadas por espacios.') };
    }
  }

  // 3. Location structure: if 8+ chars and no spaces, must not be unspaced gibberish
  if (fieldName === 'location') {
    if (trimmed.length >= 8 && !trimmed.includes(' ')) {
      return { isValid: false, error: tr('projects.validation.locationStructure', 'La ubicación debe describir una dirección o zona válida con palabras separadas por espacios.') };
    }
  }

  // 4. Consecutive consonants (5 or more consonants in a row is impossible in Spanish/English)
  if (/[bcdfghjklmnñpqrstvwxyzBCDFGHJKLMNÑPQRSTVWXYZ]{5,}/i.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.consonantCluster', 'Contiene combinaciones de consonantes continuas no legibles.') };
  }

  // 5. Vowel ratio check (between 20% and 80% for text with 6+ letters)
  if (letters.length >= 6) {
    const vowels = trimmed.match(/[aeiouáéíóúAEIOUÁÉÍÓÚ]/gi) || [];
    const ratio = vowels.length / letters.length;
    if (ratio < 0.20 || ratio > 0.80) {
      return { isValid: false, error: tr('projects.validation.vowelRatio', 'El texto debe contener una proporción legible de vocales y consonantes.') };
    }
  }

  // 6. Keyboard mash / Home row spam check:
  if (letters.length >= 8) {
    if (letters.length >= 10 && uniqueLetters.size <= 4) {
      return { isValid: false, error: tr('projects.validation.keyboardMash', 'El texto parece una combinación aleatoria o repetitiva del teclado.') };
    }
    const homeRowKeys = new Set(['a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l']);
    const homeLetters = letters.filter(l => homeRowKeys.has(l));
    if (letters.length >= 10 && (homeLetters.length / letters.length) >= 0.88) {
      return { isValid: false, error: tr('projects.validation.homeRowSpam', 'El texto contiene patrones repetitivos de teclas del teclado.') };
    }
  }

  // 7. Repetitive sub-patterns (e.g. asdasd, dfsdfs, etc.)
  if (/(.{2,5})\1{2,}/i.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.repetitivePattern', 'Contiene patrones o secuencias repetitivas de caracteres.') };
  }

  // 8. Keyboard sequences and pure repetition (e.g. asdf, qwer, zxcv, asdasd)
  const cleanAlpha = trimmed.toLowerCase().replace(/[^a-záéíóúñü]/gi, '');
  const KEYBOARD_SEQUENCES = ['asdf', 'qwer', 'zxcv', 'hjkl', 'yuio', 'uiop', 'ghjk', 'fdsa', 'rewq', 'vcxz'];
  if (KEYBOARD_SEQUENCES.some(seq => cleanAlpha.includes(seq))) {
    return { isValid: false, error: tr('projects.validation.keyboardMash', 'El texto contiene secuencias de teclas del teclado (ej. asdf).') };
  }
  if (cleanAlpha.length >= 4 && /^(.{2,4})\1+$/.test(cleanAlpha)) {
    return { isValid: false, error: tr('projects.validation.repetitivePattern', 'El texto contiene secuencias repetitivas del teclado.') };
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
  if (trimmed.length < 4) {
    return { isValid: false, error: tr('projects.validation.locationMinLength', 'La ubicación debe tener al menos 4 caracteres.') };
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
  const vowels = trimmed.match(/[aeiouáéíóúAEIOUÁÉÍÓÚ]/i);
  if (!vowels) {
    return { isValid: false, error: tr('projects.validation.locationVowels', 'La ubicación debe ser una dirección o zona legible y contener al menos una vocal.') };
  }
  if (!/^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s.,\-#'&()/°ºª]+$/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.locationInvalid', 'La ubicación contiene caracteres no permitidos.') };
  }
  return checkTextLegibility(trimmed, 'location', t);
}

/**
 * Validator for project description.
 */
export function validateProjectDescription(description, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  if (!description || typeof description !== 'string' || !description.trim()) {
    return { isValid: false, error: tr('projects.validation.descriptionRequired', 'La descripción del proyecto es obligatoria.') };
  }
  const trimmed = description.trim();
  if (trimmed.length < 10) {
    return { isValid: false, error: tr('projects.validation.descriptionMinLength', 'La descripción debe tener al menos 10 caracteres.') };
  }
  if (trimmed.length > 500) {
    return { isValid: false, error: tr('projects.validation.descriptionMaxLength', 'La descripción no puede exceder los 500 caracteres.') };
  }
  if (/[<>]/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.descriptionNoHtml', 'La descripción no puede contener etiquetas ni caracteres HTML (<, >).') };
  }
  if (/(.)\1{3,}/i.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.descriptionRepetitive', 'La descripción no puede contener caracteres repetitivos continuos.') };
  }
  const letters = trimmed.match(/[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]/g);
  if (!letters || letters.length < 5) {
    return { isValid: false, error: tr('projects.validation.descriptionInvalid', 'La descripción debe contener al menos 5 letras y ser un texto descriptivo comprensible.') };
  }
  const vowels = trimmed.match(/[aeiouáéíóúAEIOUÁÉÍÓÚ]/i);
  if (!vowels) {
    return { isValid: false, error: tr('projects.validation.descriptionVowels', 'La descripción debe ser un texto legible y contener vocales.') };
  }
  return checkTextLegibility(trimmed, 'description', t);
}

/**
 * Comprehensive validator for client full name.
 */
export function validateClientFullName(fullName, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  if (!fullName || typeof fullName !== 'string' || !fullName.trim()) {
    return { isValid: false, error: tr('clients.validation.fullNameRequired', 'El nombre completo es obligatorio.') };
  }
  const trimmed = fullName.trim();
  if (trimmed.length < 3) {
    return { isValid: false, error: tr('clients.validation.fullNameMinLength', 'El nombre completo debe tener al menos 3 caracteres.') };
  }
  if (trimmed.length > 100) {
    return { isValid: false, error: tr('clients.validation.fullNameMaxLength', 'El nombre completo no puede exceder los 100 caracteres.') };
  }
  if (/[<>]/.test(trimmed)) {
    return { isValid: false, error: tr('projects.validation.nameNoHtml', 'El nombre no puede contener etiquetas ni caracteres HTML (<, >).') };
  }
  if (!/^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s.\-']+$/.test(trimmed)) {
    return { isValid: false, error: tr('clients.validation.fullNameInvalid', 'El nombre completo solo puede contener letras y caracteres válidos.') };
  }
  const letters = trimmed.match(/[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]/g);
  if (!letters || letters.length < 3) {
    return { isValid: false, error: tr('clients.validation.fullNameInvalid', 'El nombre completo debe contener al menos 3 letras.') };
  }
  return checkTextLegibility(trimmed, 'name', t);
}

/**
 * Comprehensive validator for client email.
 * Checks RFC standard format, minimum lengths, no HTML,
 * no repetitive characters, no keyboard sequences/mash in username or domain,
 * and valid domain structure.
 */
export function validateClientEmail(email, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  if (!email || typeof email !== 'string' || !email.trim()) {
    return { isValid: false, error: tr('clients.validation.emailRequired', 'El correo electrónico es obligatorio.') };
  }
  const trimmed = email.trim().toLowerCase();

  // Basic length constraints
  if (trimmed.length < 6) {
    return { isValid: false, error: tr('clients.validation.emailInvalid', 'El correo electrónico debe tener al menos 6 caracteres.') };
  }
  if (trimmed.length > 100) {
    return { isValid: false, error: tr('clients.validation.emailInvalid', 'El correo electrónico no puede exceder los 100 caracteres.') };
  }

  // HTML / injection check
  if (/[<>]/.test(trimmed)) {
    return { isValid: false, error: tr('clients.validation.emailNoHtml', 'El correo electrónico no puede contener etiquetas HTML (<, >).') };
  }

  // RFC regex check
  if (!EMAIL_REGEX.test(trimmed)) {
    return { isValid: false, error: tr('clients.validation.emailInvalid', 'Ingrese un correo electrónico válido (ej. usuario@empresa.com).') };
  }

  const parts = trimmed.split('@');
  if (parts.length !== 2) {
    return { isValid: false, error: tr('clients.validation.emailInvalid', 'Ingrese un correo electrónico válido con formato usuario@dominio.com.') };
  }

  const [username, domain] = parts;

  // Username validation
  if (username.length < 2) {
    return { isValid: false, error: tr('clients.validation.emailUserTooShort', 'El usuario del correo electrónico debe tener al menos 2 caracteres.') };
  }

  // Continuous repetitive chars (e.g. aaaa@...)
  if (/(.)\1{3,}/i.test(username)) {
    return { isValid: false, error: tr('clients.validation.emailRepetitive', 'El correo electrónico no puede contener caracteres repetitivos continuos.') };
  }

  // Keyboard mash / sequences in username (e.g. asdf, qwer, zxcv)
  const cleanUserAlpha = username.replace(/[^a-z]/gi, '');
  const KEYBOARD_SEQUENCES = ['asdf', 'qwer', 'zxcv', 'hjkl', 'yuio', 'uiop', 'ghjk', 'fdsa', 'rewq', 'vcxz'];
  if (KEYBOARD_SEQUENCES.some(seq => cleanUserAlpha.includes(seq))) {
    return { isValid: false, error: tr('clients.validation.emailKeyboardMash', 'El correo electrónico contiene secuencias de teclas del teclado (ej. asdf).') };
  }

  // Pure repetitive sequence pattern in username (e.g. asdasdasd, ababab) or repetitive keyboard loops
  const REPETITIVE_KEYBOARD_PATTERNS = ['asdasd', 'adadad', 'dfdfdf', 'jkjkjk', 'ababab', 'testtest'];
  if (REPETITIVE_KEYBOARD_PATTERNS.some(pat => cleanUserAlpha.includes(pat)) ||
      (cleanUserAlpha.length >= 6 && /^(.{2,3})\1{2,}$/.test(cleanUserAlpha))) {
    return { isValid: false, error: tr('clients.validation.emailRepetitive', 'El correo electrónico contiene secuencias repetitivas.') };
  }

  // Username legibility: must contain at least one vowel or digit
  if (!/[aeiou0-9]/i.test(username)) {
    return { isValid: false, error: tr('clients.validation.emailInvalid', 'El usuario del correo debe ser un texto legible y contener al menos una vocal o número.') };
  }

  // Domain validation
  const domainParts = domain.split('.');
  if (domainParts.length < 2 || domainParts.some(p => p.length < 2)) {
    return { isValid: false, error: tr('clients.validation.emailInvalidDomain', 'El dominio del correo debe tener una estructura válida (ej. empresa.com).') };
  }

  const domainName = domainParts[0];
  if (KEYBOARD_SEQUENCES.some(seq => domainName.includes(seq))) {
    return { isValid: false, error: tr('clients.validation.emailInvalidDomain', 'El dominio del correo contiene secuencias del teclado no válidas.') };
  }

  if (/(.)\1{3,}/i.test(domainName)) {
    return { isValid: false, error: tr('clients.validation.emailInvalidDomain', 'El dominio del correo contiene caracteres repetitivos no válidos.') };
  }

  return { isValid: true, error: null };
}

/**
 * Comprehensive validator for client address (optional, but if entered must be valid).
 */
export function validateClientAddress(address, t = null) {
  const tr = (key, fallback) => (t ? t(key) : fallback);
  if (!address || typeof address !== 'string' || !address.trim()) {
    return { isValid: true, error: null };
  }
  const trimmed = address.trim();
  if (trimmed.length < 4) {
    return { isValid: false, error: tr('clients.validation.addressMinLength', 'La dirección debe tener al menos 4 caracteres.') };
  }
  if (trimmed.length > 150) {
    return { isValid: false, error: tr('clients.validation.addressMaxLength', 'La dirección no puede exceder los 150 caracteres.') };
  }
  if (/[<>]/.test(trimmed)) {
    return { isValid: false, error: tr('clients.validation.addressNoHtml', 'La dirección no puede contener etiquetas ni caracteres HTML (<, >).') };
  }
  if (!/^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑüÜ\s.,\-#'&()/°ºª]+$/.test(trimmed)) {
    return { isValid: false, error: tr('clients.validation.addressInvalid', 'La dirección contiene caracteres no permitidos.') };
  }
  const letters = trimmed.match(/[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ]/g);
  if (!letters || letters.length < 3) {
    return { isValid: false, error: tr('clients.validation.addressInvalid', 'La dirección debe contener al menos 3 letras.') };
  }
  return checkTextLegibility(trimmed, 'location', t);
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
