import { test, expect } from '@playwright/test';

// IAM form feedback: field-specific guidance where it helps, generic messages
// where specificity would aid an attacker. Registration errors name the field
// (no oracle risk: the backend answers 201 even for duplicates). Login server
// errors stay generic so brute force learns nothing about which half failed.
test('IAM registration names each failing field', async ({ page }) => {
  await page.goto('/iam/register-builder');

  // Step 1 empty: every account field complains by name.
  await page.getByRole('button', { name: /^next$/i }).click();
  await expect(page.getByText('El correo electrónico es obligatorio.')).toBeVisible();
  await expect(page.getByText('La contraseña es obligatoria.')).toBeVisible();
  await expect(page.getByText('Debe confirmar su contraseña.')).toBeVisible();

  // Step 1 filled: reach step 2, submit it empty.
  const stamp = Date.now();
  await page.locator('#email').fill(`feedback.${stamp}@example.test`);
  await page.locator('#password input').fill('secret123');
  await page.locator('#confirmPassword input').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();

  // Step 2 empty: every profile field complains by name.
  await expect(page.getByText('El nombre completo es obligatorio.')).toBeVisible();
  await expect(page.getByText('El nombre de usuario es obligatorio.')).toBeVisible();
  await expect(page.getByText('La dirección es obligatoria.')).toBeVisible();
  await expect(page.getByText('La edad es obligatoria.')).toBeVisible();
  await expect(page.getByText('El número de teléfono es obligatorio.')).toBeVisible();
});

test('IAM login failure is generic and reveals nothing', async ({ page }) => {
  await page.goto('/iam/login');
  await page.locator('#email').fill(`nobody.${Date.now()}@example.test`);
  await page.locator('#password input').fill('wrong-password');
  await page.locator('button[type="submit"]').click();

  // Generic message, still on the login route, no field blamed.
  await expect(page.getByText('Correo o contraseña incorrectos.')).toBeVisible();
  await expect(page).toHaveURL(/login/);
  await expect(page.locator('.p-error')).toHaveCount(0);
});
