import { describe, it, expect, vi, beforeEach } from 'vitest';

// Convergent Testing G0/G1: executable Frontend/API contract for subscriptions.
// Proves the client requests match SubscriptionsEndpoints.cs:
//   GET /api/v1/plans, GET /api/v1/plans/{id}
//   POST /api/v1/subscriptions/payments/sessions -> 201
//   PATCH /api/v1/subscriptions/payments/sessions/{id} -> 200
//   GET /api/v1/subscriptions/payments/invoices?builderId=
//   POST /api/v1/subscriptions/{id}/cancel
// No network is used; axios is mocked and only call arguments are asserted.

vi.mock('axios', () => {
  const create = vi.fn((config) => ({
    config,
    post: vi.fn(() => Promise.resolve({ data: {} })),
    get: vi.fn(() => Promise.resolve({ data: [] })),
    put: vi.fn(() => Promise.resolve({ data: {} })),
    patch: vi.fn(() => Promise.resolve({ data: {} })),
    delete: vi.fn(() => Promise.resolve({ data: {} })),
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  }));
  return { default: { create } };
});

async function lastInstance() {
  const axios = (await import('axios')).default;
  const instance = axios.create.mock.results[axios.create.mock.results.length - 1].value;
  return { instance, baseURL: instance.config?.baseURL };
}

describe('Subscriptions frontend/API contract (Convergent Testing G0/G1)', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('VITE_API_URL', '/api/v1');
    vi.stubEnv('VITE_PLANS_ENDPOINT_PATH', '/plans');
    vi.stubEnv('VITE_SUBSCRIPTIONS_ENDPOINT_PATH', '/subscriptions');
  });

  it('SUBSCRIPTIONS.BROWSE targets GET /api/v1/plans', async () => {
    const { PlanApi } = await import('../../src/subscriptions/infrastructure/plan-api.js');
    const api = new PlanApi();
    const { instance, baseURL } = await lastInstance();
    expect(baseURL).toBe('/api/v1');

    await api.getAllPlans();

    expect(instance.get).toHaveBeenCalledTimes(1);
    const [url] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/plans');
  });

  it('SUBSCRIPTIONS.BROWSE targets GET /api/v1/plans/{id}', async () => {
    const { PlanApi } = await import('../../src/subscriptions/infrastructure/plan-api.js');
    await new PlanApi().getPlanById(3);
    const { instance, baseURL } = await lastInstance();

    expect(instance.get).toHaveBeenCalledTimes(1);
    const [url] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/plans/3');
  });

  it('SUBSCRIPTIONS.PURCHASE targets POST /api/v1/subscriptions/payments/sessions', async () => {
    const { SubscriptionApi } = await import('../../src/subscriptions/infrastructure/subscription-api.js');
    const api = new SubscriptionApi();
    const { instance, baseURL } = await lastInstance();

    await api.createCheckoutSession(1, 3);

    expect(instance.post).toHaveBeenCalledTimes(1);
    const [url, payload] = instance.post.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/subscriptions/payments/sessions');
    expect(payload).toMatchObject({ builderId: 1, planId: 3 });
    expect(payload.successUrl).toContain('success=true');
    expect(payload.cancelUrl).toContain('canceled=true');
  });

  it('SUBSCRIPTIONS.PURCHASE confirms via PATCH /api/v1/subscriptions/payments/sessions/{id}', async () => {
    const { SubscriptionApi } = await import('../../src/subscriptions/infrastructure/subscription-api.js');
    const api = new SubscriptionApi();
    const { instance, baseURL } = await lastInstance();

    await api.confirmPayment(1, 'cs_test_123');

    expect(instance.patch).toHaveBeenCalledTimes(1);
    const [url, payload] = instance.patch.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/subscriptions/payments/sessions/cs_test_123');
    expect(payload).toMatchObject({ builderId: 1, status: 'confirmed' });
  });

  it('SUBSCRIPTIONS.INVOICES targets GET /api/v1/subscriptions/payments/invoices?builderId=', async () => {
    const { SubscriptionApi } = await import('../../src/subscriptions/infrastructure/subscription-api.js');
    const api = new SubscriptionApi();
    const { instance, baseURL } = await lastInstance();

    await api.getInvoicesByBuilder(1);

    expect(instance.get).toHaveBeenCalledTimes(1);
    const [url, config] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/subscriptions/payments/invoices');
    expect(config?.params).toMatchObject({ builderId: 1 });
  });

  it('SUBSCRIPTIONS.CANCEL targets POST /api/v1/subscriptions/{id}/cancel', async () => {
    const { SubscriptionApi } = await import('../../src/subscriptions/infrastructure/subscription-api.js');
    const api = new SubscriptionApi();
    const { instance, baseURL } = await lastInstance();

    await api.cancelSubscription(7);

    expect(instance.post).toHaveBeenCalledTimes(1);
    const [url] = instance.post.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/subscriptions/7/cancel');
  });

  it('PlanAssembler maps features array, featuresJson, and garbage safely', async () => {
    const { PlanAssembler } = await import('../../src/subscriptions/infrastructure/plan.assembler.js');

    const fromArray = PlanAssembler.toEntityFromResource({ id: 3, name: 'Enterprise', price: 1299, features: ['a', 'b'] });
    expect(fromArray.features).toEqual(['a', 'b']);

    const fromJson = PlanAssembler.toEntityFromResource({ id: 3, name: 'Enterprise', price: 1299, featuresJson: '["x"]' });
    expect(fromJson.features).toEqual(['x']);

    const garbage = PlanAssembler.toEntityFromResource({ id: 3, name: 'Enterprise', price: 1299, featuresJson: '{broken' });
    expect(garbage.features).toEqual([]);

    expect(PlanAssembler.toEntityFromResource(null)).toBeNull();
    expect(PlanAssembler.toEntitiesFromResourceArray('nope')).toEqual([]);
  });
});
