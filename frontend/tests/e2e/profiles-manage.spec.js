import { test, expect } from '@playwright/test';

// PROFILES.MANAGE per actor (Builder and Owner).
// Convergent Testing G2 with the IAM handoff the journey depends on:
// registration creates the profile, so each test registers through the UI
// first and then proves the profile view shows that same data, updates it,
// and keeps it after a full reload (durability, not local state).
async function registerViaUi(page, role, email, name, username) {
  const stamp = Date.now();
  await page.goto(`/iam/register-${role}`);
  await page.locator('#email').fill(email);
  await page.locator('#password input').fill('secret123');
  await page.locator('#confirmPassword input').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill(name);
  await page.locator('#username').fill(username);
  await page.locator('#address').fill('Av. Seed 100');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654310');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(new RegExp(`register-${role}`), { timeout: 20_000 });
  return stamp;
}

async function proveProfileManage(page, name, newAddress) {
  await page.goto('/profiles/profile');

  // The IAM handoff: registration data is visible in the profile view.
  await expect(page.locator('.profile-name').first()).toHaveText(name, { timeout: 20_000 });

  // Update the address through the UI (4th input: name, email, phone, address).
  await page.locator('.edit-button').first().click();
  const addressInput = page.locator('.account-card .info-group input').nth(3);
  await addressInput.fill(newAddress);
  await page.locator('.edit-actions .edit-button').click();

  // Full reload: the change survived in durable storage, not memory.
  await page.reload();
  await expect(page.locator('.profile-name').first()).toHaveText(name, { timeout: 20_000 });
  await expect(page.locator('.account-card .info-group input').nth(3)).toHaveValue(newAddress);
}

test('PROFILES Builder: registration data manages through view, update, reload', async ({ page }) => {
  const stamp = Date.now();
  await registerViaUi(page, 'builder', `e2e.prof.b.${stamp}@example.test`, 'E2E Prof Builder', `e2eprofb${String(stamp).slice(-6)}`);

  // Builders need an active subscription to reach the profile route (router
  // gate): seed it via API for the same user instead of re-buying (purchase
  // itself is proven by the subscriptions journey).
  const me = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const seeded = await page.request.post('/api/v1/subscriptions', {
    headers: { Authorization: `Bearer ${token}` },
    data: { builderId: me.id, planId: 1, startDate: new Date().toISOString(), endDate: null },
  });
  expect(seeded.status()).toBe(201);

  await proveProfileManage(page, 'E2E Prof Builder', 'Av. Persist 200');
});

test('PROFILES Owner: registration data manages through view, update, reload', async ({ page }) => {
  const stamp = Date.now();
  await registerViaUi(page, 'owner', `e2e.prof.o.${stamp}@example.test`, 'E2E Prof Owner', `e2eprofo${String(stamp).slice(-6)}`);
  await proveProfileManage(page, 'E2E Prof Owner', 'Av. Persist 300');
});
