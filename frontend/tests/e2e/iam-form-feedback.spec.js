import { test, expect } from '@playwright/test';

// IAM form feedback: field-specific guidance where it helps, generic messages
// where specificity would aid an attacker. Registration errors name the field
// (no oracle risk: the backend answers 201 even for duplicates). Login server
// errors stay generic so brute force learns nothing about which half failed.
test('IAM registration names each failing field', async ({ page }) => {
  await page.goto('/iam/register-builder');

  // Step 1 empty: every account field complains by name.
  await page.getByRole('button', { name: /^next$/i }).click();
  await expect(page.getByText('Email is required.')).toBeVisible();
  await expect(page.getByText('Password is required.')).toBeVisible();
  await expect(page.getByText('Please confirm your password.')).toBeVisible();

  // Step 1 filled: reach step 2, submit it empty.
  const stamp = Date.now();
  await page.locator('#email').fill(`feedback.${stamp}@example.test`);
  await page.locator('#password').fill('secret123');
  await page.locator('#confirmPassword').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();

  // Step 2 empty: every profile field complains by name.
  await expect(page.getByText('Company name is required.')).toBeVisible();
  await expect(page.getByText('Username is required.')).toBeVisible();
  await expect(page.getByText('Address is required.')).toBeVisible();
  await expect(page.getByText('Years in business is required.')).toBeVisible();
  await expect(page.getByText('Phone number is required.')).toBeVisible();
});

test('IAM Owner registration requires an assigned unit in UI and backend', async ({ page }) => {
  const email = `unassigned.${Date.now()}@example.test`;
  await page.goto('/iam/register-owner');
  await page.locator('#email').fill(email);
  await page.locator('#password').fill('secret123');
  await page.locator('#confirmPassword').fill('secret123');

  await expect(page.getByText('The builder must assign a unit to your email before you can register as an owner.').first()).toBeVisible();
  const nextButton = page.getByRole('button', { name: /^next$/i });
  await expect(nextButton).toBeDisabled();
  await expect(nextButton).toHaveCSS('background-color', 'rgb(156, 163, 175)');
  await expect(page.locator('#email')).toBeVisible();
  await expect(page.locator('#name')).toBeHidden();

  // Bypass the frontend and prove the backend is authoritative too.
  const response = await page.request.post('/api/v1/users', {
    data: { email, password: 'secret123', role: 'Owner' },
  });
  expect(response.status()).toBe(403);
  expect(await response.json()).toMatchObject({ code: 'owner_unit_assignment_required' });

  const login = await page.request.post('/api/v1/sessions', {
    data: { email, password: 'secret123' },
  });
  expect(login.status()).toBe(401);
});

test('IAM Owner registration fails closed when unit assignment cannot be verified', async ({ page }) => {
  await page.route('**/api/v1/authentication/invitation**', route => route.abort());
  await page.goto('/iam/register-owner');
  await page.locator('#email').fill(`verification-unavailable.${Date.now()}@example.test`);
  await page.locator('#password').fill('secret123');
  await page.locator('#confirmPassword').fill('secret123');

  await expect(page.getByText('Could not verify the assigned unit. Please try again.').first()).toBeVisible();
  await expect(page.getByRole('button', { name: /^next$/i })).toBeDisabled();
  await expect(page.locator('#email')).toBeVisible();
  await expect(page.locator('#name')).toBeHidden();
});

test('IAM login failure is generic and reveals nothing', async ({ page }) => {
  await page.goto('/iam/login');
  await expect(page.getByLabel('Password', { exact: true })).toBeVisible();
  await page.locator('#email').fill(`nobody.${Date.now()}@example.test`);
  await page.locator('#password').fill('wrong-password');
  await page.locator('button[type="submit"]').click();

  // Generic message, still on the login route, no field blamed.
  await expect(page.getByText('Invalid email or password.')).toBeVisible();
  await expect(page).toHaveURL(/login/);
  await expect(page.locator('.p-error')).toHaveCount(0);
});
