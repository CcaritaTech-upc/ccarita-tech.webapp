import { describe, it, expect, vi, beforeEach } from 'vitest';

// Convergent Testing G0/G1: executable Frontend/API contract for devices.
// Proves the client requests match DevicesEndpoints.cs:
//   GET /api/v1/devices, GET /api/v1/devices?unitId=, GET /api/v1/devices/{id}
//   POST /api/v1/devices, PUT /api/v1/devices/{id}, DELETE /api/v1/devices/{id}
//   GET /api/v1/devices/types
//   POST /api/v1/devices/{id}/commands {attribute, value} (ADR-B5 key names)
//   GET /api/v1/devices/{id}/status
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

describe('Devices frontend/API contract (Convergent Testing G0/G1)', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('VITE_API_URL', '/api/v1');
    vi.stubEnv('VITE_DEVICES_ENDPOINT_PATH', '/devices');
  });

  it('DEVICES.BROWSE targets GET /api/v1/devices and its types catalog', async () => {
    const { DeviceApi } = await import('../../src/devices/infrastructure/device-api.js');
    const api = new DeviceApi();
    const { instance, baseURL } = await lastInstance();

    await api.getAllDevices();
    await api.getDeviceTypes();

    const urls = instance.get.mock.calls.map(([url]) => `${baseURL}${url}`);
    expect(urls).toContain('/api/v1/devices');
    expect(urls).toContain('/api/v1/devices/types');
  });

  it('DEVICES.BROWSE filters by unit via GET /api/v1/devices?unitId=', async () => {
    const { DeviceApi } = await import('../../src/devices/infrastructure/device-api.js');
    const api = new DeviceApi();
    const { instance, baseURL } = await lastInstance();

    await api.getDevicesByUnitId(415);

    const [url] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toContain('/api/v1/devices');
    expect(url).toContain('unitId=415');
  });

  it('DEVICES.CONTROL sends commands with verbatim catalog keys', async () => {
    const { DeviceApi } = await import('../../src/devices/infrastructure/device-api.js');
    const api = new DeviceApi();
    const { instance, baseURL } = await lastInstance();

    await api.sendCommand(431, 'brightness', 80);

    expect(instance.post).toHaveBeenCalledTimes(1);
    const [url, payload] = instance.post.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/devices/431/commands');
    expect(payload).toMatchObject({ attribute: 'brightness', value: 80 });
    expect(payload).not.toHaveProperty('attributeName');
  });

  it('DEVICES.CONTROL reads status via GET /api/v1/devices/{id}/status', async () => {
    const { DeviceApi } = await import('../../src/devices/infrastructure/device-api.js');
    const api = new DeviceApi();
    const { instance, baseURL } = await lastInstance();

    await api.getDeviceStatus(431);

    const [url] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/devices/431/status');
  });

  it('DEVICES.MANAGE creates, updates, and deletes through the item routes', async () => {
    const { DeviceApi } = await import('../../src/devices/infrastructure/device-api.js');
    const api = new DeviceApi();
    const { instance, baseURL } = await lastInstance();

    await api.createDevice({ name: 'Hall Light' });
    await api.updateDevice({ id: 431, name: 'Renamed' });
    await api.deleteDevice(431);

    const postUrl = instance.post.mock.calls[0][0];
    const [putUrl] = instance.put.mock.calls[0];
    const [deleteUrl] = instance.delete.mock.calls[0];
    expect(`${baseURL}${postUrl}`).toBe('/api/v1/devices');
    expect(`${baseURL}${putUrl}`).toBe('/api/v1/devices/431');
    expect(`${baseURL}${deleteUrl}`).toBe('/api/v1/devices/431');
  });
});
