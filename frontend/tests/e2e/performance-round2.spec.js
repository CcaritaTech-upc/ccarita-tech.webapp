import { test, expect } from '@playwright/test';
import crypto from 'node:crypto';
import { provisionAssignedOwner } from './owner-assignment.fixture.js';

async function session(page, role) {
  const stamp = `${Date.now()}${crypto.randomInt(100, 1000)}`;
  const email = `e2e.round2.${role}.${stamp}@example.test`;
  if (role === 'Owner') await provisionAssignedOwner(page, email, stamp);
  const registration = await page.request.post('/api/v1/users', { data: { email, password: 'secret123', role } });
  expect(registration.status()).toBe(201);
  const response = await page.request.post('/api/v1/sessions', { data: { email, password: 'secret123' } });
  expect(response.status()).toBe(201);
  const actor = await response.json();
  const headers = { Authorization: `Bearer ${actor.token}` };
  const profile = await page.request.post('/api/v1/profiles', { headers, data: { userId: actor.id, name: `Round Two ${role}`, username: `round${stamp}`, address: 'Av. Round Two 100', age: 30, phoneNumber: '+51987654310', photoUrl: '', secondEmail: '' } });
  expect(profile.status()).toBe(201);
  if (role === 'Builder') {
    const subscription = await page.request.post('/api/v1/subscriptions', { headers, data: { builderId: actor.id, planId: 1, startDate: new Date().toISOString(), endDate: null } });
    expect(subscription.status()).toBe(201);
  }
  await page.goto('/iam/register-builder');
  await page.evaluate(actor => { localStorage.setItem('token', actor.token); localStorage.setItem('currentUser', JSON.stringify({ id: actor.id, email: actor.email, role: actor.role })); }, actor);
}

for (const role of ['Builder', 'Owner']) {
  test(`ROUND2 ${role}: base routes and real menu reach canonical screens`, async ({ page }) => {
    await session(page, role);
    await page.goto('/analytics');
    await expect(page).toHaveURL(/\/analytics\/dashboard$/);
    await expect(page.locator(`.${role.toLowerCase()}-dashboard .stats-grid`)).toBeVisible();
    if (role === 'Builder') {
      await page.goto('/subscriptions');
      await expect(page).toHaveURL(/\/subscriptions\/my-subscription$/);
      await expect(page.locator('.hero-plan-title')).toBeVisible();
    }
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await page.locator('.sidebar-menu a[href="/profiles/profile"]').click();
    await expect(page).toHaveURL(/\/profiles\/profile$/);
    await expect(page.locator('.profile-name')).toHaveText(`Round Two ${role}`);
    await page.getByRole('button', { name: 'Open navigation' }).click();
    await page.locator('.sidebar-menu a[href="/analytics/dashboard"]').click();
    await expect(page.locator(`.${role.toLowerCase()}-dashboard .stats-grid`)).toBeVisible();
    if (role === 'Builder') {
      await page.getByRole('button', { name: 'Open navigation' }).click();
      await page.locator('.sidebar-menu a[href="/subscriptions/my-subscription"]').click();
      await expect(page.locator('.plans-grid')).toBeVisible();
    }
  });

  test(`ROUND2 ${role}: profile controls have labels and preserve edit/language behavior`, async ({ page }) => {
    await session(page, role);
    await page.goto('/profiles/profile');
    await expect(page.getByLabel('Full Name', { exact: true })).toHaveValue(`Round Two ${role}`);
    for (const label of ['Email', 'Phone Number', 'Address', 'Alternate Email']) await expect(page.getByLabel(label, { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Notifications' })).toBeVisible();
    for (const [foreground, background] of [
      ['.custom-green-select .p-select-label', '.custom-green-select'],
      ['.logout-button .p-button-label', '.custom-toolbar'],
      ['.edit-button .p-button-label', '.edit-button'],
    ]) {
      const ratio = await page.evaluate(([foreground, background]) => {
        const luminance = color => {
          const channels = color.match(/[\d.]+/g).slice(0, 3).map(Number).map(c => { const v = c / 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; });
          return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
        };
        const a = luminance(getComputedStyle(document.querySelector(foreground)).color);
        const b = luminance(getComputedStyle(document.querySelector(background)).backgroundColor);
        return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
      }, [foreground, background]);
      expect(ratio, foreground).toBeGreaterThanOrEqual(4.5);
    }
    await page.getByRole('button', { name: 'Edit Profile', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Cambiar foto' })).toBeVisible();
    await page.getByLabel('Address', { exact: true }).fill('Av. Round Two persisted');
    await page.getByRole('button', { name: 'Save Changes', exact: true }).click();
    await expect(page.getByLabel('Address', { exact: true })).toHaveAttribute('readonly', '');
    await page.reload();
    await expect(page.getByLabel('Address', { exact: true })).toHaveValue('Av. Round Two persisted');
    await page.locator('.account-card').getByLabel('App Language', { exact: true }).selectOption('es');
    await expect(page.getByLabel('Nombre completo', { exact: true })).toHaveValue(`Round Two ${role}`);
  });
}

for (const role of ['builder', 'owner']) test(`ROUND2 registration ${role}: semantic tabs, native clipboard data, persistent step values`, async ({ page }) => {
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  const email = `e2e.clipboard.${role}.${Date.now()}@example.test`;
  if (role === 'owner') await provisionAssignedOwner(page, email, `${Date.now()}clipboard`);
  await page.goto(`/iam/register-${role}`);
  await expect(page.locator('link[rel="preconnect"][href="https://fonts.googleapis.com"]')).toHaveCount(1);
  await expect(page.locator('link[rel="preconnect"][href="https://fonts.gstatic.com"][crossorigin]')).toHaveCount(1);
  const tabs = page.getByRole('tablist');
  await expect(tabs.getByRole('tab')).toHaveCount(2);
  await expect(tabs.getByRole('tabpanel')).toHaveCount(0);
  await expect(tabs.getByRole('tab').first()).toHaveAttribute('aria-selected', 'true');
  await expect(tabs.getByRole('tab').last()).toHaveAttribute('aria-selected', 'false');
  await page.locator('#email').fill(email);
  await page.locator('#password').fill('secret123');
  await page.locator('#confirmPassword').fill('secret123');
  await page.getByRole('button', { name: /^next$/i }).click();
  await expect(tabs.getByRole('tab').last()).toHaveAttribute('aria-selected', 'true');
  const age = page.locator(role === 'builder' ? '#yearsInBusiness' : '#age input');
  await age.evaluate(input => { const data = new DataTransfer(); data.setData('text', '30'); input.dispatchEvent(new ClipboardEvent('paste', { bubbles: true, cancelable: true, clipboardData: data })); });
  await expect(age).toHaveValue('30');
  await page.locator('#name').fill('Clipboard Builder');
  await page.getByRole('button', { name: /^back$/i }).click();
  await page.getByRole('button', { name: /^next$/i }).click();
  await expect(page.locator('#name')).toHaveValue('Clipboard Builder');
  await expect(age).toHaveValue('30');
  expect(errors).toEqual([]);
});
