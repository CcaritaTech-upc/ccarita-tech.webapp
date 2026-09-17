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

  // Pay Starter. Simulated backends redirect back with a same-origin session
  // id; a real Stripe backend lands on checkout.stripe.com, where the test
  // pays with the standard test card and returns to the same success URL.
  await page.getByRole('button', { name: /elegir starter/i }).click();
  await payOnStripeIfRedirected(page, email);

  // The view confirms and cleans the URL when done. Assert on those durable
  // outcomes, never on the 2.5s toast that may come and go before polling.
  await expect(page).not.toHaveURL(/session_id=/, { timeout: 30_000 });
  await expect(page.locator('.hero-plan-title')).toHaveText('Starter', { timeout: 20_000 });
  await expect(page.locator('.status-active').first()).toBeVisible();
});

// If checkout redirected to real Stripe, pay with the test card and come back.
// Simulated mode never leaves our origin, so this is a no-op there.
// Field names were read off the live checkout DOM (single elements iframe):
// cardNumber, cardExpiry, cardCvc, plus the required email.
async function payOnStripeIfRedirected(page, email) {
  await page.waitForURL(/session_id=|checkout\.stripe\.com/, { timeout: 20_000 });
  if (!page.url().includes('checkout.stripe.com')) return;

  const yy = String(new Date().getFullYear() + 2).slice(-2);
  // The elements iframe loads after navigation: poll until the card field exists.
  let cardFrame = null;
  await expect.poll(async () => {
    for (const frame of page.frames()) {
      if (await frame.locator('input[name="cardNumber"]').count()) {
        cardFrame = frame;
        return true;
      }
    }
    return false;
  }, { timeout: 20_000 }).toBe(true);

  await cardFrame.locator('input[name="email"]').fill(email);
  await cardFrame.locator('input[name="cardNumber"]').fill('4242424242424242');
  await cardFrame.locator('input[name="cardExpiry"]').fill(`12${yy}`);
  await cardFrame.locator('input[name="cardCvc"]').fill('123');
  await cardFrame.locator('input[name="billingName"]').fill('E2E Tester');
  await expect(cardFrame.locator('input[name="cardNumber"]')).toHaveValue(/4242/);
  await page.getByRole('button', { name: /^pay$/i }).click();
}
