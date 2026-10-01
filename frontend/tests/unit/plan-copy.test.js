import { describe, expect, it } from 'vitest';
import { translatePlanDescription, translatePlanFeature } from '../../src/subscriptions/presentation/plan-copy.js';

describe('subscription plan copy localization', () => {
  const messages = {
    'subscriptions.planCatalog.starter.description': 'Perfecto para proyectos pequeños',
    'subscriptions.planCatalog.starter.features.devices': 'Hasta 50 dispositivos IoT',
  };
  const t = key => messages[key];
  const te = key => Object.hasOwn(messages, key);

  it('translates seeded descriptions using normalized plan names', () => {
    expect(translatePlanDescription({ name: ' Starter ', description: 'API description' }, t, te))
      .toBe('Perfecto para proyectos pequeños');
  });

  it('translates known seeded features and preserves unknown feature text', () => {
    expect(translatePlanFeature('STARTER', 'Up to 50 IoT devices', t, te)).toBe('Hasta 50 dispositivos IoT');
    expect(translatePlanFeature('Starter', 'Custom onboarding', t, te)).toBe('Custom onboarding');
  });

  it('falls back to API copy when a translation key is unavailable', () => {
    expect(translatePlanDescription({ name: 'Custom', description: 'Custom plan' }, t, te)).toBe('Custom plan');
    expect(translatePlanDescription({ name: 'Starter' }, t, () => false)).toBe('');
  });
});
