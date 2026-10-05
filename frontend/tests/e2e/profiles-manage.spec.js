import { test, expect } from '@playwright/test';
import { provisionAssignedOwner } from './owner-assignment.fixture.js';

// PROFILES.MANAGE per actor (Builder and Owner).
// Convergent Testing G2 with the IAM handoff the journey depends on:
// registration creates the profile, so each test registers through the UI
// first and then proves the profile view shows that same data, updates it,
// and keeps it after a full reload (durability, not local state).
async function registerViaUi(page, role, email, name, username) {
  const stamp = Date.now();
  await page.goto(`/iam/register-${role}`);
  await page.locator('#email').fill(email);
  await page.locator('#password').fill('secret123');
  await page.locator('#confirmPassword').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill(name);
  await page.locator('#username').fill(username);
  await page.locator('#address').fill('Av. Seed 100');
  await page.locator(role === 'builder' ? '#yearsInBusiness' : '#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654310');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(new RegExp(`register-${role}`), { timeout: 20_000 });
  return stamp;
}

async function seedBuilderSubscription(page) {
  const me = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const token = await page.evaluate(() => localStorage.getItem('token'));
  const seeded = await page.request.post('/api/v1/subscriptions', {
    headers: { Authorization: `Bearer ${token}` },
    data: { builderId: me.id, planId: 1, startDate: new Date().toISOString(), endDate: null },
  });
  expect(seeded.status()).toBe(201);
}

async function proveProfileManage(page, name, newAddress, yearsInBusiness = null) {
  await page.goto('/profiles/profile');

  // The IAM handoff: registration data is visible in the profile view.
  await expect(page.locator('.profile-name').first()).toHaveText(name, { timeout: 20_000 });
  const updatedYearsInBusiness = yearsInBusiness === null ? null : yearsInBusiness + 1;
  if (yearsInBusiness !== null) {
    await expect(page.getByLabel('Years in Business', { exact: true })).toHaveValue(String(yearsInBusiness));
  }

  // Update the address through the UI (4th input: name, email, phone, address).
  await page.locator('.edit-button').first().click();
  const addressInput = page.locator('.account-card .info-group input').nth(3);
  await addressInput.fill(newAddress);
  if (updatedYearsInBusiness !== null) {
    await page.getByLabel('Years in Business', { exact: true }).fill(String(updatedYearsInBusiness));
  }
  const saved = page.waitForResponse(
    (r) => r.url().includes('/api/v1/profiles/') && r.request().method() === 'PUT');
  await page.locator('.edit-actions .edit-button').click();
  await saved;
  await expect(page.getByText(/Profile updated successfully\.|Perfil actualizado correctamente/)).toBeVisible();

  // Full reload: the change survived in durable storage, not memory.
  await page.reload();
  await expect(page.locator('.profile-name').first()).toHaveText(name, { timeout: 20_000 });
  await expect(page.locator('.account-card .info-group input').nth(3)).toHaveValue(newAddress);
  if (updatedYearsInBusiness !== null) {
    await expect(page.getByLabel('Years in Business', { exact: true })).toHaveValue(String(updatedYearsInBusiness));
  }
}

test('PROFILES Builder: registration data manages through view, update, reload', async ({ page }) => {
  const stamp = Date.now();
  await registerViaUi(page, 'builder', `e2e.prof.b.${stamp}@example.test`, 'E2E Prof Builder', `e2eprofb${String(stamp).slice(-6)}`);

  // Builders need an active subscription to reach the profile route (router
  // gate): seed it via API for the same user instead of re-buying (purchase
  // itself is proven by the subscriptions journey).
  await seedBuilderSubscription(page);

  await proveProfileManage(page, 'E2E Prof Builder', 'Av. Persist 200', 30);
});

test('PROFILES Owner: registration data manages through view, update, reload', async ({ page }) => {
  const stamp = Date.now();
  const email = `e2e.prof.o.${stamp}@example.test`;
  await provisionAssignedOwner(page, email, stamp);
  await registerViaUi(page, 'owner', email, 'E2E Prof Owner', `e2eprofo${String(stamp).slice(-6)}`);
  await proveProfileManage(page, 'E2E Prof Owner', 'Av. Persist 300');
});

for (const role of ['builder', 'owner']) {
  test(`PROFILES ${role}: failed update shows error and preserves unsaved edits`, async ({ page }) => {
    const stamp = Date.now();
    const email = `e2e.prof.fail.${role}.${stamp}@example.test`;

    if (role === 'owner') {
      await provisionAssignedOwner(page, email, stamp);
    }
    await registerViaUi(page, role, email, `E2E Prof ${role}`, `e2eprof${role}${String(stamp).slice(-5)}`);
    if (role === 'builder') await seedBuilderSubscription(page);

    await page.goto('/profiles/profile');
    await expect(page.locator('.profile-name').first()).toHaveText(`E2E Prof ${role}`, { timeout: 20_000 });
    await page.locator('.edit-button').first().click();
    await page.locator('.account-card .info-group input').nth(3).fill(`Unsaved Address ${stamp}`);

    await page.route('**/api/v1/profiles/**', async (route) => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ message: 'Simulated profile update failure' }),
        });
        return;
      }
      await route.continue();
    });

    const failedUpdate = page.waitForResponse(
      (response) => response.url().includes('/api/v1/profiles/') && response.request().method() === 'PUT');
    await page.locator('.edit-actions .edit-button').click();
    expect((await failedUpdate).status()).toBe(500);

    await expect(page.getByText(/Could not update the profile\. Please try again\.|No se pudo actualizar el perfil\. Inténtalo de nuevo\./)).toBeVisible();
    await expect(page.getByText(/Profile updated successfully\.|Perfil actualizado correctamente/)).toHaveCount(0);
    await expect(page.locator('.edit-actions .edit-button')).toBeVisible();

    await page.locator('.edit-actions .cancel-button').click();
    await expect(page.locator('.account-card .info-group input').nth(3)).toHaveValue('Av. Seed 100');
  });
}
