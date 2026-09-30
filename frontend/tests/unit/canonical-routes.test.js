import { describe, expect, it } from 'vitest';
import { createMemoryHistory, createRouter } from 'vue-router';
import analyticsRoutes from '../../src/analytics/presentation/analytics-routes.js';
import subscriptionsRoutes from '../../src/subscriptions/presentation/subscriptions-routes.js';

describe('canonical nested entry routes', () => {
  it.each([
    ['/analytics', '/analytics/dashboard', 'analytics-dashboard'],
    ['/subscriptions', '/subscriptions/my-subscription', 'my-subscription'],
  ])('%s resolves to its real nested screen', async (entry, canonical, name) => {
    const router = createRouter({ history: createMemoryHistory(), routes: [
      { path: '/analytics', children: analyticsRoutes.map(r => ({ ...r, component: r.component ? {} : undefined })) },
      { path: '/subscriptions', children: subscriptionsRoutes.map(r => ({ ...r, component: r.component ? {} : undefined })) },
    ] });
    await router.push(entry);
    expect(router.currentRoute.value.path).toBe(canonical);
    expect(router.currentRoute.value.name).toBe(name);
  });
});
