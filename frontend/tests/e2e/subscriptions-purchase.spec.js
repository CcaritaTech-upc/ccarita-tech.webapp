import { test, expect } from '@playwright/test';

// SUBSCRIPTIONS.PURCHASE.HAPPY_PATH (Builder)
// Convergent Testing G2: the purchasing actor's journey through the deployed
// system. Simulated Stripe never leaves the app: checkout returns a same-origin
// success URL, the view confirms, and the plan activates.
test('SUBSCRIPTIONS Builder happy path: browse plans, pay, active subscription', async ({ page }) => {
  const stamp = Date.now();
  const email = `e2e.subs.${stamp}@example.test`;
  const password = 'secret123';

  // Builder account via the same stepper the Builder journey proves.
  await page.goto('/iam/register-builder');
  await page.locator('#email').fill(email);
  await page.locator('#password input').fill(password);
  await page.locator('#confirmPassword input').fill(password);
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill('E2E Subs');
  await page.locator('#username').fill(`e2esubs${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 789');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654323');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(/register-builder/, { timeout: 20_000 });

  // Browse seeded plans.
  await page.goto('/subscriptions/my-subscription');
  await expect(page.locator('.plans-grid')).toContainText('Starter', { timeout: 20_000 });

  // Pay Starter: simulated checkout redirects back with a session id.
  await page.getByRole('button', { name: /elegir starter/i }).click();

  // Simulated checkout redirects back with a session id; the view confirms and
  // cleans the URL when done. Assert on those durable outcomes, never on the
  // 2.5s toast that may come and go before the first poll.
  await expect(page).toHaveURL(/session_id=cs_sim_/, { timeout: 20_000 });
  await expect(page).not.toHaveURL(/session_id=/, { timeout: 20_000 });
  await expect(page.locator('.hero-plan-title')).toHaveText('Starter', { timeout: 20_000 });
  await expect(page.locator('.status-active').first()).toBeVisible();
});
