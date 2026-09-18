import { test, expect } from '@playwright/test';

// ANALYTICS.VIEW per actor (Builder and Owner).
// Convergent Testing G2: each role sees its own dashboard shape through the
// deployed system. Data-level scoping (403/404) is proven versioned at G1;
// here the UI renders the right dashboard and the metrics endpoint carries
// the builder's own project.
test('ANALYTICS Builder: dashboard renders with own project metrics', async ({ page }) => {
  const stamp = Date.now();
  await page.goto('/iam/register-builder');
  await page.locator('#email').fill(`e2e.an.b.${stamp}@example.test`);
  await page.locator('#password input').fill('secret123');
  await page.locator('#confirmPassword input').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill('E2E An Builder');
  await page.locator('#username').fill(`e2eanb${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 300');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654314');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(/register-builder/, { timeout: 20_000 });

  const me = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const auth = { Authorization: `Bearer ${token}` };
  const created = await page.request.post('/api/v1/projects', {
    headers: auth,
    data: { name: `E2E Metrics ${stamp}`, description: 'D', location: 'L', totalUnits: 1, builderId: me.id, imageUrl: null },
  });
  expect(created.status()).toBe(201);

  // Builders need an active subscription to reach analytics (router gate).
  const seeded = await page.request.post('/api/v1/subscriptions', {
    headers: auth,
    data: { builderId: me.id, planId: 1, startDate: new Date().toISOString(), endDate: null },
  });
  expect(seeded.status()).toBe(201);

  await page.goto('/analytics/dashboard');
  await expect(page.locator('.builder-dashboard')).toBeVisible({ timeout: 20_000 });
  await expect(page.locator('.builder-dashboard .stats-grid')).toBeVisible();

  const metrics = await (await page.request.get(`/api/v1/analytics/builders/${me.id}/metrics`, { headers: auth })).json();
  expect(metrics.activeProjectsCount).toBeGreaterThanOrEqual(1);
});

test('ANALYTICS Owner: dashboard renders the owner view', async ({ page }) => {
  const stamp = Date.now();
  await page.goto('/iam/register-owner');
  await page.locator('#email').fill(`e2e.an.o.${stamp}@example.test`);
  await page.locator('#password input').fill('secret123');
  await page.locator('#confirmPassword input').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill('E2E An Owner');
  await page.locator('#username').fill(`e2eano${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 301');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654315');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(/register-owner/, { timeout: 20_000 });

  await page.goto('/analytics/dashboard');
  await expect(page.locator('.owner-dashboard')).toBeVisible({ timeout: 20_000 });
  await expect(page.locator('.owner-dashboard .stats-grid')).toBeVisible();
});
