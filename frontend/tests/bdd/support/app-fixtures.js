import { expect } from '@playwright/test';
import { provisionAssignedOwner } from '../../e2e/owner-assignment.fixture.js';

export const TEST_PASSWORD = 'secret123';

function makeSuffix() {
  return `${Date.now()}${Math.floor(Math.random() * 1000).toString().padStart(3, '0')}`;
}

export async function registerTestActor(page, role, alias, {
  name,
  phoneNumber = '+51987654321',
  activeSubscription = true,
  subscriptionPlanId = 1,
} = {}) {
  const suffix = makeSuffix();
  const normalizedRole = role.toLowerCase();
  const email = `bdd.${normalizedRole}.${alias}.${suffix}@example.test`;
  const profileName = name || `BDD ${role} ${alias}`;

  if (normalizedRole === 'owner') {
    await provisionAssignedOwner(page, email, suffix);
  }

  await page.goto(`/iam/register-${normalizedRole}`);
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(TEST_PASSWORD);
  await page.locator('#confirmPassword').fill(TEST_PASSWORD);
  await page.getByRole('button', { name: /next|siguiente/i }).click();
  await page.locator('#name').fill(profileName);
  await page.locator('#username').fill(`bdd${normalizedRole}${suffix}`);
  await page.locator('#address').fill('Av. BDD 100');

  if (normalizedRole === 'builder') {
    await page.locator('#yearsInBusiness').fill('10');
  } else {
    await page.locator('#age input').fill('30');
  }

  await page.locator('#phoneNumber').fill(phoneNumber);
  await page.getByRole('button', { name: /register|registr|create|crear|save|guardar|submit/i }).click();
  await expect(page).not.toHaveURL(new RegExp(`register-${normalizedRole}`), { timeout: 20_000 });

  if (normalizedRole === 'builder' && activeSubscription) {
    await seedBuilderSubscription(page, subscriptionPlanId);
  }

  return { role: normalizedRole, alias, email, name: profileName, password: TEST_PASSWORD };
}

export async function seedBuilderSubscription(page, planId = 1) {
  const currentUser = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const response = await page.request.post('/api/v1/subscriptions', {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      builderId: currentUser.id,
      planId,
      startDate: new Date().toISOString(),
      endDate: null,
    },
  });

  expect(response.status()).toBe(201);
}

export async function createTestProject(page, { name, description, location, totalUnits = 1 }) {
  const currentUser = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const response = await page.request.post('/api/v1/projects', {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      name,
      description: description || 'Valid project description for automated tests',
      location: location || 'Av. Primavera 123, Lima',
      totalUnits,
      builderId: currentUser.id,
      imageUrl: null,
    },
  });
  const responseBody = await response.text();
  expect(response.status(), `Project setup failed: ${responseBody}`).toBe(201);
  return JSON.parse(responseBody);
}

export async function defineTestProjectStructure(page, projectId, { floors = 1, unitsPerFloor = 1 } = {}) {
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const response = await page.request.post(`/api/v1/projects/${projectId}/structure`, {
    headers: { Authorization: `Bearer ${token}` },
    data: { floors, unitsPerFloor, floorNumbers: null },
  });
  const responseBody = await response.text();
  expect(response.status(), `Project structure setup failed: ${responseBody}`).toBe(201);
  return responseBody ? JSON.parse(responseBody) : null;
}

export async function createTestClient(page, { fullName, email, projectId, projectName, accountStatement = 'Active' }) {
  const currentUser = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const response = await page.request.post('/api/v1/clients', {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      fullName,
      projectName,
      accountStatement,
      builderId: currentUser.id,
      projectId,
      email,
      phoneNumber: '+51987654321',
      address: 'Av. Primavera 456',
    },
  });
  const responseBody = await response.text();
  expect(response.status(), `Client setup failed: ${responseBody}`).toBe(201);
  return JSON.parse(responseBody);
}

export async function clearBrowserSession(page) {
  await page.evaluate(() => {
    const language = localStorage.getItem('lang');
    localStorage.clear();
    if (language) localStorage.setItem('lang', language);
  });
  await page.context().clearCookies();
}
