import { test, expect } from '@playwright/test';

// DEVICES.CONTROL.HAPPY_PATH (Owner)
// Convergent Testing G2 across bounded contexts, as the product works: a
// builder provisions project, unit, client record, and device via API; the
// owner registers with the invited email (IAM auto-links unit, projections,
// and device); the owner then drives brightness from the UI and the deployed
// stack reflects it on the device status endpoint.
test('DEVICES Owner happy path: provision, register, command, reflected status', async ({ page }) => {
  const stamp = Date.now();
  const ownerEmail = `e2e.dev.o.${stamp}@example.test`;
  const password = 'secret123';

  // Builder account via UI (owns the session for provisioning calls).
  await page.goto('/iam/register-builder');
  await page.locator('#email').fill(`e2e.dev.b.${stamp}@example.test`);
  await page.locator('#password input').fill(password);
  await page.locator('#confirmPassword input').fill(password);
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill('E2E Dev Builder');
  await page.locator('#username').fill(`e2edev${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 100');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654311');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(/register-builder/, { timeout: 20_000 });

  const builder = await page.evaluate(() => JSON.parse(localStorage.getItem('currentUser')));
  const builderToken = await page.evaluate(() => localStorage.getItem('token'));
  const auth = { Authorization: `Bearer ${builderToken}` };

  // Provision: project → unit → client invitation → unit device.
  const project = await (await page.request.post('/api/v1/projects', {
    headers: auth,
    data: { name: 'E2E Towers', description: 'D', location: 'L', totalUnits: 1, builderId: builder.id, imageUrl: null },
  })).json();
  const unit = await (await page.request.post('/api/v1/units', {
    headers: auth,
    data: { projectId: project.id, unitNumber: 'D1', floor: 1, roomNumber: 'D1' },
  })).json();
  const clientResp = await page.request.post('/api/v1/clients', {
    headers: auth,
    data: {
      fullName: 'E2E Dev Owner', projectName: 'E2E Towers', accountStatement: 'Pending',
      builderId: builder.id, projectId: project.id, email: ownerEmail,
      phoneNumber: '+51987654312', address: 'Av. E2E 101', unitId: unit.id, unitNumber: 'D1',
    },
  });
  expect(clientResp.status()).toBe(201);
  const unitId = unit.id;
  const projectId = project.id;

  // Owner registers with the invited email: IAM links unit and device.
  await page.evaluate(() => localStorage.clear());
  await page.context().clearCookies();
  await page.goto('/iam/register-owner');
  await page.locator('#email').fill(ownerEmail);
  await page.locator('#password input').fill(password);
  await page.locator('#confirmPassword input').fill(password);
  await page.getByRole('button', { name: /^next$/i }).click();
  await page.locator('#name').fill('E2E Dev Owner');
  await page.locator('#username').fill(`e2edevo${String(stamp).slice(-6)}`);
  await page.locator('#address').fill('Av. E2E 101');
  await page.locator('#age input').fill('30');
  await page.locator('#phoneNumber').fill('+51987654312');
  await page.getByRole('button', { name: /register|create|save|submit/i }).click();
  await expect(page).not.toHaveURL(/register-owner/, { timeout: 20_000 });

  // The owner adds their custom unit device (owner-custom path: Owner role
  // plus the auto-linked unit ownership), then drives it from the UI.
  const ownerToken = await page.evaluate(() => localStorage.getItem('token'));
  const ownerAuth = { Authorization: `Bearer ${ownerToken}` };
  const device = await (await page.request.post('/api/v1/devices', {
    headers: ownerAuth,
    data: { name: 'E2E Hall Light', type: 'SmartLight', location: 'Hall', projectId, unitId, status: 'online' },
  })).json();
  expect(device.id).toBeGreaterThan(0);

  // Drive brightness from the owner device table.
  await page.goto('/devices/device-management');
  await expect(page.locator('.unit-devices-table')).toContainText('E2E Hall Light', { timeout: 20_000 });
  const slider = page.locator('.control-panel input.native-slider').first();
  await slider.fill('80');
  await page.locator('.send-btn').first().click();
  await expect(page.getByText('Command sent').first()).toBeVisible({ timeout: 20_000 });

  // The deployed stack reflects the desired state on the device status endpoint.
  const status = await (await page.request.get(`/api/v1/devices/${device.id}/status`, {
    headers: ownerAuth,
  })).json();
  expect(status.deviceId).toBe(device.id);
  expect(JSON.stringify(status.desired)).toContain('80');
});
