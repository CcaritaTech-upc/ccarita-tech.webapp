import { expect } from '@playwright/test';

export async function provisionAssignedOwner(page, ownerEmail, suffix) {
  const builderEmail = `e2e.provision.${suffix}@example.test`;
  const projectNameSuffix = String(suffix).replace(/(.)\1{3,}/g, '$1$1');
  const password = 'secret123';

  const registration = await page.request.post('/api/v1/users', {
    data: { email: builderEmail, password, role: 'Builder' },
  });
  expect(registration.status()).toBe(201);

  const sessionResponse = await page.request.post('/api/v1/sessions', {
    data: { email: builderEmail, password },
  });
  expect(sessionResponse.status()).toBe(201);
  const builder = await sessionResponse.json();
  const headers = { Authorization: `Bearer ${builder.token}` };

  const projectResponse = await page.request.post('/api/v1/projects', {
    headers,
    data: {
      name: `E2E Owner Assignment ${projectNameSuffix}`,
      description: 'E2E test project',
      location: 'E2E',
      totalUnits: 1,
      builderId: builder.id,
      imageUrl: null,
    },
  });
  const projectBody = await projectResponse.text();
  expect(projectResponse.status(), `Owner-assignment project setup failed: ${projectBody}`).toBe(201);
  const project = JSON.parse(projectBody);

  const unitResponse = await page.request.post('/api/v1/units', {
    headers,
    data: { projectId: project.id, unitNumber: 'E1', floor: 1, roomNumber: 'E1' },
  });
  expect(unitResponse.status()).toBe(201);
  const unit = await unitResponse.json();

  const clientResponse = await page.request.post('/api/v1/clients', {
    headers,
    data: {
      fullName: 'E2E Assigned Owner',
      projectName: project.name,
      accountStatement: 'Pending',
      builderId: builder.id,
      projectId: project.id,
      email: ownerEmail,
      phoneNumber: '+51987654321',
      address: 'E2E address',
      unitId: unit.id,
      unitNumber: unit.unitNumber,
    },
  });
  expect(clientResponse.status()).toBe(201);

  return { project, unit };
}
