import { test, expect } from '@playwright/test';

// IAM.REGISTRATION.HAPPY_PATH (Builder) + IAM.LOGIN.HAPPY_PATH (Builder)
// Convergent Testing G2: every served actor needs its own happy-path evidence
// at the highest applicable layer. The Owner variant lives in
// iam-happy-path.spec.js; this is the Builder variant. Same 2-step stepper
// shape under /iam, same stable-ID locators, Builder role on submit.
test('IAM Builder happy path: register, login, authorized access', async ({ page }) => {
  const stamp = Date.now();
  const email = `e2e.builder.${stamp}@example.test`;
  const password = 'secret123';

  // Step 1: account — register-builder stepper under /iam.
  await page.goto('/iam/register-builder');
  await page.locator('#email').fill(email);
  await page.locator('#password input').fill(password);
  await page.locator('#confirmPassword input').fill(password);
  await page.getByRole('button', { name: /^next$/i }).click();

  // Step 2: company — representative valid data only.
  await page.locator('#name').fill('E2E Builder');
  await page.locator('#username').fill(`e2ebuilder${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 456');
  await page.locator('#age input').fill('35');
  await page.locator('#phoneNumber').fill('+51987654322');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();

  // Registration signs the user in and leaves the registration route.
  await expect(page).not.toHaveURL(/register-builder/, { timeout: 20_000 });

  // Reset to an anonymous state to prove login from a clean session.
  await page.evaluate(() => localStorage.clear());
  await page.context().clearCookies();

  // Login with the created Builder account.
  await page.goto('/iam/login');
  await page.locator('#email').fill(email);
  await page.locator('#password input').fill(password);
  await page.locator('button[type="submit"]').click();

  // Authorized access: login leaves the public login route.
  await expect(page).not.toHaveURL(/\/login$/, { timeout: 20_000 });
});
