import { describe, it, expect } from 'vitest';
import {
  isValidEmail,
  isValidPassword,
  isValidAge,
  doPasswordsMatch,
  isNewPasswordDifferent,
  isEligible,
} from '../../src/shared/presentation/validators.js';

// IAM.REGISTRATION.HAPPY_PATH + Tier A (field, cross-field, contextual)
describe('IAM validators (Convergent Testing: domain/component ownership)', () => {
  it('IAM.REGISTRATION.HAPPY_PATH accepts a well-formed email and password', () => {
    expect(isValidEmail('owner@example.test')).toBe(true);
    expect(isValidPassword('secret123')).toBe(true);
  });

  it('IAM.REGISTRATION.INVALID_EMAIL rejects malformed emails', () => {
    expect(isValidEmail('')).toBe(false);
    expect(isValidEmail('not-an-email')).toBe(false);
    expect(isValidEmail('a@b')).toBe(false);
    expect(isValidEmail(null)).toBe(false);
  });

  it('IAM.REGISTRATION.WEAK_PASSWORD rejects short passwords', () => {
    expect(isValidPassword('12345')).toBe(false);
    expect(isValidPassword('')).toBe(false);
  });

  it('IAM.REGISTRATION.PASSWORD_MISMATCH requires confirmation to match', () => {
    expect(doPasswordsMatch('secret123', 'secret123')).toBe(true);
    expect(doPasswordsMatch('secret123', 'Secret123')).toBe(false);
    expect(doPasswordsMatch('secret123', '')).toBe(false);
  });

  it('IAM.PASSWORD_CHANGE.SAME_AS_CURRENT requires a different password', () => {
    expect(isNewPasswordDifferent('old-secret', 'new-secret')).toBe(true);
    expect(isNewPasswordDifferent('same-secret', 'same-secret')).toBe(false);
    expect(isNewPasswordDifferent('', 'new-secret')).toBe(false);
  });

  it('IAM.REGISTRATION.AGE_BOUNDARY enforces integer range', () => {
    expect(isValidAge(18)).toBe(true);
    expect(isValidAge(17)).toBe(false);
    expect(isValidAge(18.5)).toBe(false);
  });

  it('IAM.REGISTRATION.ELIGIBILITY uses business context, not a magic 18', () => {
    const policies = { housing: { PE: 18, ES: 16 } };
    const evalDate = new Date('2026-09-16T00:00:00Z');
    // 17 years old in PE/housing -> rejected; same person in ES/housing -> accepted
    expect(
      isEligible({ dateOfBirth: '2009-09-17', product: 'housing', jurisdiction: 'PE', evaluationDate: evalDate, policies }),
    ).toBe(false);
    expect(
      isEligible({ dateOfBirth: '2009-09-17', product: 'housing', jurisdiction: 'ES', evaluationDate: evalDate, policies }),
    ).toBe(true);
    expect(isEligible({ dateOfBirth: 'not-a-date' })).toBe(false);
  });
});
