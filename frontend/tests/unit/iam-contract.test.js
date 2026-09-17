import { describe, it, expect, vi, beforeEach } from 'vitest';

// Convergent Testing G1: executable Frontend/API contract for IAM.
// Proves the client request (route, method, payload shape) matches the
// backend contract in backend/src/IoBuild.Api/IAM/Interfaces/REST/IamEndpoints.cs:
//   POST /api/v1/users (RegisterUser) -> 201
//   POST /api/v1/sessions (SignIn) -> 201
//   DELETE /api/v1/sessions/current (Bearer) -> 204
//   GET /api/v1/authentication/invitation?email= -> 200
// No network is used; axios is mocked and only call arguments are asserted.

vi.mock('axios', () => {
  const instances = [];
  const create = vi.fn((config) => {
    const instance = {
      config,
      post: vi.fn(() => Promise.resolve({ data: {} })),
      get: vi.fn(() => Promise.resolve({ data: {} })),
      delete: vi.fn(() => Promise.resolve({ data: {} })),
      interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
    };
    instances.push(instance);
    return instance;
  });
  return { default: { create } };
});

async function createIamApi() {
  const axios = (await import('axios')).default;
  const { IamApi } = await import('../../src/iam/infrastructure/iam-api.js');
  const api = new IamApi();
  const instance = axios.create.mock.results[axios.create.mock.results.length - 1].value;
  const baseURL = instance.config?.baseURL;
  return { api, instance, baseURL };
}

describe('IAM frontend/API contract (Convergent Testing G1)', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('VITE_API_URL', '/api/v1');
    vi.stubEnv('VITE_USERS_ENDPOINT_PATH', '/users');
    vi.stubEnv('VITE_IAM_SESSIONS_PATH', '/sessions');
  });

  it('IAM.REGISTRATION.HAPPY_PATH targets POST /api/v1/users with email/password/role', async () => {
    const { api, instance, baseURL } = await createIamApi();
    expect(baseURL).toBe('/api/v1');

    await api.signUp({ email: 'owner@example.test', password: 'secret123', role: 'Owner' });

    expect(instance.post).toHaveBeenCalledTimes(1);
    const [url, payload] = instance.post.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/users');
    expect(payload).toMatchObject({ email: 'owner@example.test', password: 'secret123', role: 'Owner' });
  });

  it('IAM.REGISTRATION serves Builder and Owner through the same contract route', async () => {
    const { api, instance, baseURL } = await createIamApi();

    await api.signUp({ email: 'builder@example.test', password: 'secret123', role: 'Builder' });
    await api.signUp({ email: 'owner@example.test', password: 'secret123', role: 'Owner' });

    expect(instance.post).toHaveBeenCalledTimes(2);
    for (const [url, payload] of instance.post.mock.calls) {
      expect(`${baseURL}${url}`).toBe('/api/v1/users');
      expect(['Builder', 'Owner']).toContain(payload.role);
    }
  });

  it('IAM.LOGIN.HAPPY_PATH targets POST /api/v1/sessions with email/password', async () => {
    const { api, instance, baseURL } = await createIamApi();

    await api.signIn({ email: 'owner@example.test', password: 'secret123' });

    expect(instance.post).toHaveBeenCalledTimes(1);
    const [url, payload] = instance.post.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/sessions');
    expect(payload).toMatchObject({ email: 'owner@example.test', password: 'secret123' });
  });

  it('IAM.LOGOUT targets DELETE /api/v1/sessions/current to revoke the token', async () => {
    const { api, instance, baseURL } = await createIamApi();

    await api.signOut();

    expect(instance.delete).toHaveBeenCalledTimes(1);
    const [url] = instance.delete.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/sessions/current');
  });

  it('IAM.AUTHORIZED_ACCESS targets GET /api/v1/users for the user list', async () => {
    const { api, instance, baseURL } = await createIamApi();

    await api.getUsers();

    expect(instance.get).toHaveBeenCalledTimes(1);
    const [url] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/users');
  });

  it('IAM.REGISTRATION invitation check targets GET /api/v1/authentication/invitation', async () => {
    const { api, instance, baseURL } = await createIamApi();

    await api.checkInvitation('owner.auto@example.com');

    expect(instance.get).toHaveBeenCalledTimes(1);
    const [url, config] = instance.get.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/authentication/invitation');
    expect(config?.params).toMatchObject({ email: 'owner.auto@example.com' });
  });

  it('IAM.LOGIN maps the session response to the AuthenticatedUser entity shape', async () => {
    const { AuthenticatedUserAssembler } = await import(
      '../../src/iam/infrastructure/authenticated-user.assembler.js'
    );

    const entity = AuthenticatedUserAssembler.toEntityFromResponse({
      id: 7,
      email: 'owner@example.test',
      role: 'Owner',
      token: 'jwt-test-token',
    });

    expect(entity).toMatchObject({
      id: 7,
      email: 'owner@example.test',
      role: 'Owner',
      token: 'jwt-test-token',
    });
  });
});
