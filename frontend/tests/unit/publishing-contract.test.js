import { describe, it, expect, vi, beforeEach } from 'vitest';

// Convergent Testing G0/G1: executable Frontend/API contract for publishing.
// Proves the client requests match PublishingEndpoints.cs:
//   GET/POST /api/v1/projects, GET/PUT/DELETE /api/v1/projects/{id}
//   POST /api/v1/projects/{id}/structure
//   GET /api/v1/units?projectId=, PATCH /api/v1/units/{id} (owner assignment)
//   GET/POST /api/v1/clients, GET/PUT/DELETE /api/v1/clients/{id}
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

describe('Publishing frontend/API contract (Convergent Testing G0/G1)', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubEnv('VITE_API_URL', '/api/v1');
    vi.stubEnv('VITE_PROJECTS_ENDPOINT_PATH', '/projects');
    vi.stubEnv('VITE_UNITS_ENDPOINT_PATH', '/units');
    vi.stubEnv('VITE_CLIENTS_ENDPOINT_PATH', '/clients');
  });

  it('PUBLISHING.MANAGE browses and mutates projects through item routes', async () => {
    const { ProjectApi } = await import('../../src/projects/infrastructure/project-api.js');
    const api = new ProjectApi();
    const { instance, baseURL } = await lastInstance();

    await api.getProjectsByBuilderId(71);
    await api.createProject({ name: 'T', builderId: 71 });
    await api.updateProject({ id: 5, name: 'T2' });
    await api.deleteProject(5);

    const urls = [
      ...instance.get.mock.calls.map(([u]) => `GET ${baseURL}${u}`),
      ...instance.post.mock.calls.map(([u]) => `POST ${baseURL}${u}`),
      ...instance.put.mock.calls.map(([u]) => `PUT ${baseURL}${u}`),
      ...instance.delete.mock.calls.map(([u]) => `DELETE ${baseURL}${u}`),
    ];
    expect(urls).toContain('GET /api/v1/projects');
    expect(urls).toContain('POST /api/v1/projects');
    expect(urls).toContain('PUT /api/v1/projects/5');
    expect(urls).toContain('DELETE /api/v1/projects/5');
  });

  it('PUBLISHING.MANAGE defines structure once per project', async () => {
    const { ProjectApi } = await import('../../src/projects/infrastructure/project-api.js');
    const api = new ProjectApi();
    const { instance, baseURL } = await lastInstance();

    await api.defineStructure(5, { floors: 2, unitsPerFloor: 3 });

    const [url, payload] = instance.post.mock.calls[0];
    expect(`${baseURL}${url}`).toBe('/api/v1/projects/5/structure');
    expect(payload).toMatchObject({ floors: 2, unitsPerFloor: 3 });
  });

  it('PUBLISHING.MANAGE reads units by project and assigns owners', async () => {
    const { ProjectApi } = await import('../../src/projects/infrastructure/project-api.js');
    const api = new ProjectApi();
    const { instance, baseURL } = await lastInstance();

    await api.getUnitsByProject(5);
    await api.patchUnitOwner(9, 'owner@example.test');

    const [getUrl, getConfig] = instance.get.mock.calls[0];
    expect(`${baseURL}${getUrl}`).toBe('/api/v1/units');
    expect(getConfig?.params).toMatchObject({ projectId: 5 });
    const [patchUrl, patchPayload] = instance.patch.mock.calls[0];
    expect(`${baseURL}${patchUrl}`).toBe('/api/v1/units/9');
    expect(patchPayload).toMatchObject({ ownerEmail: 'owner@example.test' });
  });

  it('PUBLISHING.MANAGE manages clients through collection and item routes', async () => {
    const { ClientApi } = await import('../../src/clients/infrastructure/client-api.js');
    const api = new ClientApi();
    const { instance, baseURL } = await lastInstance();

    await api.getClientsByProjectId(5);
    await api.createClient({ fullName: 'Acme', builderId: 71 });
    await api.updateClient({ id: 3, fullName: 'Acme Intl' });
    await api.deleteClient(3);

    const urls = [
      ...instance.get.mock.calls.map(([u]) => `GET ${baseURL}${u}`),
      ...instance.post.mock.calls.map(([u]) => `POST ${baseURL}${u}`),
      ...instance.put.mock.calls.map(([u]) => `PUT ${baseURL}${u}`),
      ...instance.delete.mock.calls.map(([u]) => `DELETE ${baseURL}${u}`),
    ];
    expect(urls).toContain('GET /api/v1/clients');
    expect(urls).toContain('POST /api/v1/clients');
    expect(urls).toContain('PUT /api/v1/clients/3');
    expect(urls).toContain('DELETE /api/v1/clients/3');
  });
});
