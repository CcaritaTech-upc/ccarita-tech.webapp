import { describe, it, expect, vi, beforeEach } from 'vitest';

// Convergent Testing G0/G1: executable Frontend/API contract for profiles.
// Proves the client requests match ProfilesEndpoints.cs:
//   GET /api/v1/profiles?userId= (array, first wins)
//   POST /api/v1/profiles
//   PUT /api/v1/profiles/{id}
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

function stubLocalStorage(user) {
  const store = { currentUser: user ? JSON.stringify(user) : null, token: user ? 't' : null };
  vi.stubGlobal('localStorage', {
    getItem: (k) => (k in store ? store[k] : null),
    setItem: () => {},
    removeItem: () => {},
  });
}

async function lastInstance() {
  const axios = (await import('axios')).default;
  const instance = axios.create.mock.results[axios.create.mock.results.length - 1].value;
  return { instance, baseURL: instance.config?.baseURL };
}

describe('Profiles frontend/API contract (Convergent Testing G0/G1)', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('VITE_API_URL', '/api/v1');
    vi.stubEnv('VITE_PROFILES_ENDPOINT_PATH', '/profiles');
    vi.unstubAllGlobals();
  });

  it('PROFILES.MANAGE fetches by user id via GET /api/v1/profiles?userId=', async () => {
    const { ProfileApi } = await import('../../src/profiles/infrastructure/profile-api.js');
    const api = new ProfileApi();
    const { instance, baseURL } = await lastInstance();

    await api.getProfile(31);

    expect(instance.get).toHaveBeenCalledTimes(1);
    const [url, config] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/profiles');
    expect(config?.params).toMatchObject({ userId: 31 });
  });

  it('PROFILES.MANAGE requires a user id before any request', async () => {
    const { ProfileApi } = await import('../../src/profiles/infrastructure/profile-api.js');
    const api = new ProfileApi();
    await expect(api.getProfile(undefined)).rejects.toThrow('User ID is required');
    await expect(api.updateProfile(0, {})).rejects.toThrow('Profile ID is required');
  });

  it('PROFILES.MANAGE creates via POST /api/v1/profiles', async () => {
    const { ProfileApi } = await import('../../src/profiles/infrastructure/profile-api.js');
    const api = new ProfileApi();
    const { instance, baseURL } = await lastInstance();

    await api.createProfile({ userId: 31, name: 'N', username: 'n31' });

    const [url, payload] = instance.post.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/profiles');
    expect(payload).toMatchObject({ userId: 31 });
  });

  it('PROFILES.MANAGE updates via PUT /api/v1/profiles/{id}', async () => {
    const { ProfileApi } = await import('../../src/profiles/infrastructure/profile-api.js');
    const api = new ProfileApi();
    const { instance, baseURL } = await lastInstance();

    await api.updateProfile(9, { name: 'New' });

    const [url, payload] = instance.put.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/profiles/9');
    expect(payload).toMatchObject({ name: 'New' });
  });

  it('ProfileAssembler takes email and role from IAM, profile fields from the resource', async () => {
    stubLocalStorage({ id: 31, email: 'owner31@example.test', role: 'Owner' });
    const { ProfileAssembler } = await import('../../src/profiles/infrastructure/profile.assembler.js');

    const { profileEntity } = ProfileAssembler.toDomainFromResponse({
      id: 9, userId: 31, name: 'Name', username: 'user31', age: 30, photoUrl: 'https://img.test/a',
    });

    expect(profileEntity.email).toBe('owner31@example.test');
    expect(profileEntity.role).toBe('Owner');
    expect(profileEntity.name).toBe('Name');
    expect(profileEntity.photoUrl).toBe('https://img.test/a');
  });

  it('ProfileAssembler falls back to builder role without a session', async () => {
    stubLocalStorage(null);
    const { ProfileAssembler } = await import('../../src/profiles/infrastructure/profile.assembler.js');

    const { profileEntity } = ProfileAssembler.toDomainFromResponse({ id: 9, userId: 31 });
    expect(profileEntity.role).toBe('builder');
    expect(profileEntity.email).toBe('');
  });
});
