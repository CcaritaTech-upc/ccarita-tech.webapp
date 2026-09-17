import { test, expect } from '@playwright/test';

// PUBLISHING.MANAGE.HAPPY_PATH (Builder)
// Convergent Testing G2: the builder creates a project through the UI form,
// defines its structure once (via API; the dialog matrix is covered by
// validation unit tests and the versioned structure proofs), and the UI
// reflects the provisioned units.
test('PUBLISHING Builder happy path: create project, define structure, see units', async ({ page }) => {
  const stamp = Date.now();
  const projectName = `E2E Towers ${stamp}`;

  // Builder account via UI.
  await page.goto('/iam/register-builder');
  await page.locator('#email').fill(`e2e.pub.${stamp}@example.test`);
  await page.locator('#password input').fill('secret123');
  await page.locator('#confirmPassword input').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill('E2E Pub Builder');
  await page.locator('#username').fill(`e2epubb${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 200');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654313');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(/register-builder/, { timeout: 20_000 });

  // Builders need an active subscription to reach project routes (router gate):
  // seed one via API for the same builder (purchase itself is proven elsewhere).
  const seedToken = await page.evaluate(() => localStorage.getItem('token'));
  const seedMe = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const seeded = await page.request.post('/api/v1/subscriptions', {
    headers: { Authorization: `Bearer ${seedToken}` },
    data: { builderId: seedMe.id, planId: 1, startDate: new Date().toISOString(), endDate: null },
  });
  expect(seeded.status()).toBe(201);

  // Create the project through the UI form.
  await page.goto('/projects/new');
  const fields = page.locator('form .form-field input');
  await fields.nth(0).fill(projectName);
  await page.locator('form textarea').fill('E2E description');
  await fields.nth(1).fill('E2E District');
  await page.locator('form button[type="submit"], form .custom-green-button').first().click();

  // Find the created project id through the API for the same builder.
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const auth = { Authorization: `Bearer ${token}` };
  let projectId = 0;
  await expect.poll(async () => {
    const list = await (await page.request.get('/api/v1/projects', { headers: auth })).json();
    const mine = list.find((p) => p.name === projectName);
    if (mine) projectId = mine.id;
    return projectId;
  }, { timeout: 20_000 }).toBeGreaterThan(0);

  // Define structure once (1 floor x 1 unit) and prove the UI reflects it.
  const structure = await page.request.post(`/api/v1/projects/${projectId}/structure`, {
    headers: auth,
    data: { floors: 1, unitsPerFloor: 1, floorNumbers: null },
  });
  expect(structure.status()).toBe(201);

  await page.goto(`/projects/${projectId}`);
  await expect(page.locator('.unit-card').first()).toBeVisible({ timeout: 20_000 });

  // The management grid lists the new project.
  await page.goto('/projects');
  await expect(page.locator('.project-grid')).toContainText(projectName, { timeout: 20_000 });
});
