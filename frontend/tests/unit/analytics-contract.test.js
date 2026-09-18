import { describe, it, expect, vi, beforeEach } from 'vitest';

// Convergent Testing G0/G1: executable Frontend/API contract for analytics.
// Proves the client requests match AnalyticsEndpoints.cs:
//   GET /api/v1/analytics/builders/{id}/metrics|energy
//   GET /api/v1/analytics/owners/{id}/metrics|energy
//   GET /api/v1/analytics/insights?projectId=&metric=
// No network is used; axios is mocked and only call arguments are asserted.

vi.mock('axios', () => {
  const create = vi.fn((config) => ({
    config,
    post: vi.fn(() => Promise.resolve({ data: {} })),
    get: vi.fn(() => Promise.resolve({ data: {} })),
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

describe('Analytics frontend/API contract (Convergent Testing G0/G1)', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('VITE_API_URL', '/api/v1');
    vi.stubEnv('VITE_ANALYTICS_ENDPOINT_PATH', '/analytics');
    vi.stubEnv('VITE_DEVICES_ENDPOINT_PATH', '/devices');
  });

  it('ANALYTICS.VIEW fetches dashboards per role', async () => {
    const { AnalyticsApi } = await import('../../src/analytics/infrastructure/analytics-api.js');
    const api = new AnalyticsApi();
    const { instance, baseURL } = await lastInstance();
    const dashboard = {
      data: {
        totalDevices: 0, onlineDevices: 0, offlineDevices: 0, alertsCount: 0,
        activeProjectsCount: 0, totalUnits: 0, occupiedUnits: 0, occupancyRate: 0,
        energyEfficiencyAvg: 0, temperatureHistory: [], energyHistory: [],
        hourlyEnergyData: [], monthlyOccupancy: [], devicesByType: [], projectsOverview: [],
        dailyEnergyConsumption: [], waterUsageWeekly: [],
      },
    };
    instance.get.mockResolvedValueOnce(dashboard).mockResolvedValueOnce(dashboard);

    await api.getDashboardMetrics(91, 'builder');
    await api.getDashboardMetrics(93, 'owner');

    const urls = instance.get.mock.calls.map(([u]) => `${baseURL}${u}`);
    expect(urls).toContain('/api/v1/analytics/builders/91/metrics');
    expect(urls).toContain('/api/v1/analytics/owners/93/metrics');
  });

  it('ANALYTICS.VIEW fetches live energy per role with a window', async () => {
    const { AnalyticsApi } = await import('../../src/analytics/infrastructure/analytics-api.js');
    const api = new AnalyticsApi();
    const { instance, baseURL } = await lastInstance();

    await api.getLiveEnergy(91, 'builder', 5);
    await api.getLiveEnergy(93, 'owner', 5);

    const calls = instance.get.mock.calls.map(([u, c]) => [`${baseURL}${u}`, c?.params]);
    expect(calls).toContainEqual(['/api/v1/analytics/builders/91/energy', { minutes: 5 }]);
    expect(calls).toContainEqual(['/api/v1/analytics/owners/93/energy', { minutes: 5 }]);
  });

  it('ANALYTICS.VIEW fetches insights scoped by project', async () => {
    const { AnalyticsApi } = await import('../../src/analytics/infrastructure/analytics-api.js');
    const api = new AnalyticsApi();
    const { instance, baseURL } = await lastInstance();
    instance.get.mockResolvedValueOnce({ data: [] });

    await api.getAnalyticalInsights(91, 'temperature');

    const [url] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toContain('/api/v1/analytics/insights');
    expect(url).toContain('projectId=91');
  });
});
