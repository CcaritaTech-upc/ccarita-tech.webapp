import { test, expect } from '@playwright/test';
import { provisionAssignedOwner } from './owner-assignment.fixture.js';

// IAM.REGISTRATION.HAPPY_PATH + IAM.LOGIN.HAPPY_PATH
// Convergent Testing: system/E2E proves the user-visible outcome only.
// Exhaustive validation lives in lower layers (vitest/xUnit).
// Resilient locators: stable IDs first, roles second. No getByLabel on PrimeVue wrappers.
test('IAM happy path: register, login, authorized access, logout, revoked token', async ({ page }) => {
  const stamp = Date.now();
  const email = `e2e.${stamp}@example.test`;
  const password = 'secret123';

  await provisionAssignedOwner(page, email, stamp);

  // Step 1: account — register-owner is a 2-step stepper under /iam.
  await page.goto('/iam/register-owner');
  await expect(page.locator('#password')).toHaveAccessibleName('Password *');
  await expect(page.locator('#confirmPassword')).toHaveAccessibleName('Confirm Password *');
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('#confirmPassword').fill(password);
  await expect(page.getByRole('button', { name: /^next$/i })).toBeEnabled();
  await page.getByRole('button', { name: /^next$/i }).click();

  // Step 2: profile — representative valid data only.
  await page.locator('#name').fill('E2E Owner');
  await page.locator('#username').fill(`e2eowner${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 123');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654321');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();

  // Registration signs the user in and leaves the registration route.
  await expect(page).not.toHaveURL(/register-owner/, { timeout: 20_000 });

  // Reset to an anonymous state to prove login from a clean session.
  await page.evaluate(() => localStorage.clear());
  await page.context().clearCookies();

  // Login with the created account.
  await page.goto('/iam/login');
  await expect(page.getByLabel('Password', { exact: true })).toBeVisible();
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);
  await page.locator('button[type="submit"]').click();

  // Authorized access: login leaves the public login route.
  await expect(page).not.toHaveURL(/\/login$/, { timeout: 20_000 });

  // Logout revokes the token server-side (business rule) and clears the session.
  const token = await page.evaluate(() => localStorage.getItem('token'));
  expect(token, 'expected a bearer token after login').toBeTruthy();
  await page.getByRole('button', { name: 'Open navigation' }).click();
  const sidebarLogout = page.locator('.sidebar-content .logout-item');
  await expect(sidebarLogout).toBeVisible();
  await sidebarLogout.click();

  // Back on a public route with no local session left.
  await expect(page).toHaveURL(/login/, { timeout: 20_000 });
  expect(await page.evaluate(() => localStorage.getItem('token'))).toBeNull();

  // The revoked token is rejected afterwards.
  const revoked = await page.request.get('/api/v1/users', {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(revoked.status()).toBe(401);
});
