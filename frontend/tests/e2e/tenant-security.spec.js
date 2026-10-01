import { test, expect } from '@playwright/test';

async function createBuilder(page, suffix) {
  const email = `tenant.${suffix}@example.test`;
  const password = 'builder-secure-123';
  const registration = await page.request.post('/api/v1/users', {
    data: { email, password, role: 'Builder' },
  });
  expect(registration.status()).toBe(201);

  const login = await page.request.post('/api/v1/sessions', { data: { email, password } });
  expect(login.status()).toBe(201);
  const user = await login.json();
  const headers = { Authorization: `Bearer ${user.token}` };
  const subscription = await page.request.post('/api/v1/subscriptions', {
    headers,
    data: { builderId: user.id, planId: 1, startDate: new Date().toISOString(), endDate: null },
  });
  expect(subscription.status()).toBe(201);
  return { user, headers };
}

async function createProject(page, builder, name) {
  const response = await page.request.post('/api/v1/projects', {
    headers: builder.headers,
    data: { name, description: 'Tenant isolation E2E', location: 'E2E', totalUnits: 1, builderId: builder.user.id, imageUrl: null },
  });
  expect(response.status()).toBe(201);
  return response.json();
}

async function createClient(page, builder, project, details) {
  const response = await page.request.post('/api/v1/clients', {
    headers: builder.headers,
    data: {
      fullName: details.fullName,
      projectName: project.name,
      accountStatement: 'Pending',
      builderId: builder.user.id,
      projectId: project.id,
      email: details.email,
      phoneNumber: '+51987654321',
      address: 'Private E2E address',
      unitId: details.unitId ?? null,
      unitNumber: details.unitNumber ?? null,
    },
  });
  expect(response.status()).toBe(201);
  return response.json();
}

test('SECURITY client lists and assignments stay inside the authenticated Builder tenant', async ({ page }) => {
  const suffix = `${Date.now()}${Math.random().toString(36).slice(2, 7)}`;
  const builderA = await createBuilder(page, `${suffix}.a`);
  const builderB = await createBuilder(page, `${suffix}.b`);
  const projectA = await createProject(page, builderA, `Tenant A ${suffix}`);
  const projectB = await createProject(page, builderB, `Tenant B ${suffix}`);

  const unitResponse = await page.request.post('/api/v1/units', {
    headers: builderB.headers,
    data: { projectId: projectB.id, unitNumber: 'B1', floor: 1, roomNumber: 'B1' },
  });
  expect(unitResponse.status()).toBe(201);
  const foreignUnit = await unitResponse.json();
  const invitedOwnerEmail = `invited.${suffix}@example.test`;

  await createClient(page, builderA, projectA, {
    fullName: 'Tenant A Client',
    email: `client.a.${suffix}@example.test`,
  });
  await createClient(page, builderB, projectB, {
    fullName: 'Tenant B Private Client',
    email: invitedOwnerEmail,
    unitId: foreignUnit.id,
    unitNumber: 'B1',
  });

  // The anonymous invitation lookup reports eligibility only; it does not expose tenant PII.
  const invitation = await page.request.get(`/api/v1/authentication/invitation?email=${encodeURIComponent(invitedOwnerEmail)}`);
  expect(invitation.status()).toBe(200);
  expect(await invitation.json()).toEqual({ assigned: true, alreadyRegistered: false });

  const ownerRegistration = await page.request.post('/api/v1/users', {
    data: { email: invitedOwnerEmail, password: 'owner-secure-123', role: 'Owner' },
  });
  expect(ownerRegistration.status()).toBe(201);
  const ownerLogin = await page.request.post('/api/v1/sessions', {
    data: { email: invitedOwnerEmail, password: 'owner-secure-123' },
  });
  expect(ownerLogin.status()).toBe(201);
  const owner = await ownerLogin.json();

  const ownList = await page.request.get('/api/v1/clients', { headers: builderA.headers });
  expect(ownList.status()).toBe(200);
  const ownListBody = await ownList.text();
  expect(ownListBody).toContain('Tenant A Client');
  expect(ownListBody).not.toContain('Tenant B Private Client');
  expect(ownListBody).not.toContain(invitedOwnerEmail);
  expect((await page.request.get('/api/v1/clients', { headers: builderB.headers })).status()).toBe(200);

  expect((await page.request.get(`/api/v1/clients?builderId=${builderB.user.id}`, { headers: builderA.headers })).status()).toBe(403);
  expect((await page.request.get(`/api/v1/clients?projectId=${projectB.id}`, { headers: builderA.headers })).status()).toBe(404);
  expect((await page.request.get('/api/v1/clients', { headers: { Authorization: `Bearer ${owner.token}` } })).status()).toBe(403);
  expect((await page.request.get('/api/v1/users', { headers: builderA.headers })).status()).toBe(403);

  const foreignProjectCreate = await page.request.post('/api/v1/clients', {
    headers: builderA.headers,
    data: { fullName: 'Cross Project', projectName: projectB.name, accountStatement: 'Pending', builderId: builderA.user.id, projectId: projectB.id },
  });
  expect(foreignProjectCreate.status()).toBe(404);
  const foreignUnitCreate = await page.request.post('/api/v1/clients', {
    headers: builderA.headers,
    data: { fullName: 'Cross Unit', projectName: projectA.name, accountStatement: 'Pending', builderId: builderA.user.id, projectId: projectA.id, unitId: foreignUnit.id, email: `cross-unit.${suffix}@example.test` },
  });
  expect(foreignUnitCreate.status()).toBe(404);

  await page.goto('/iam/login');
  await page.evaluate(user => {
    localStorage.setItem('token', user.token);
    localStorage.setItem('currentUser', JSON.stringify({ id: user.id, email: user.email, role: user.role }));
  }, builderA.user);
  await page.goto('/clients');
  await expect(page).toHaveURL(/\/clients$/);
  await expect(page.getByText('Tenant A Client')).toBeVisible();
  await expect(page.getByText('Tenant B Private Client')).toHaveCount(0);
});
