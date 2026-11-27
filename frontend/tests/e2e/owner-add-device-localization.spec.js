import { test, expect } from '@playwright/test';
import crypto from 'node:crypto';
import { provisionAssignedOwner } from './owner-assignment.fixture.js';

// DEVICES owner journey, language ES: the Add Device entry point and dialog
// carry no English text.
test('Owner sees the Add Device dialog in Spanish', async ({ page }) => {
  // Client emails reject four or more identical characters in a row.
  const suffix = `${Date.now()}${crypto.randomInt(100, 1000)}`.replace(/(.){3,}/g, '$1$1');
  const email = `ui.owner.${suffix}@example.test`;
  const password = 'ui-owner-password-123';

  // Owners can only register with an email a builder has assigned to a unit.
  await provisionAssignedOwner(page, email, suffix);
  const registration = await page.request.post('/api/v1/users', { data: { email, password, role: 'Owner' } });
  expect(registration.status()).toBe(201);
  const login = await page.request.post('/api/v1/sessions', { data: { email, password } });
  expect(login.status()).toBe(201);
  const owner = await login.json();

  await page.goto('/iam/login');
  await page.evaluate(user => {
    localStorage.setItem('lang', 'es');
    localStorage.setItem('token', user.token);
    localStorage.setItem('currentUser', JSON.stringify({ id: user.id, email: user.email, role: user.role }));
  }, owner);

  await page.goto('/devices/device-management');
  await page.getByRole('button', { name: 'Agregar Dispositivo' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Agregar Dispositivo');
  await expect(dialog).toContainText('Tipo de dispositivo');
  await expect(dialog).toContainText('Nombre del dispositivo');
  await expect(dialog).toContainText('Asignar a unidad');
  await expect(dialog.getByRole('button', { name: 'Cancelar' })).toBeVisible();
  await expect(dialog).not.toContainText(/Add Device|Device Type|Device Name|Assign to Unit|Select a|Cancel\b/);

  await dialog.getByRole('button', { name: 'Agregar Dispositivo' }).click();
  await expect(dialog).toContainText('Selecciona un tipo de dispositivo.');
  await expect(dialog).toContainText('El nombre del dispositivo debe tener entre 2 y 50 caracteres.');
});
