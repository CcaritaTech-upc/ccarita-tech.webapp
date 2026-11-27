import { test, expect } from '@playwright/test';
import { provisionAssignedOwner } from './owner-assignment.fixture.js';
import { uniqueStamp } from '../support/unique-stamp.js';

// DEVICES owner journey, language ES: the Add Device entry point and dialog
// carry no English text.
test('Owner sees the unit dashboard and Add Device dialog in Spanish', async ({ page }) => {
    const suffix = uniqueStamp();
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

  // The owner's own unit is reported as occupied, in the chosen language.
  await page.goto('/analytics/dashboard');
  const unitCard = page.locator('.unit-card').first();
  await expect(unitCard).toContainText('Piso 1 · Unidad E1');
  await expect(unitCard).toContainText('Ocupada');
  await expect(page.getByRole('heading', { name: /Energía en vivo/ })).toBeVisible();

  await page.goto('/devices/device-management');
  await page.getByRole('button', { name: 'Agregar Dispositivo' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('Agregar Dispositivo');
  // The unit is named by floor and room, not by its database id.
  await expect(dialog).toContainText('Piso 1 · Unidad E1');
  await expect(dialog).toContainText('Tipo de dispositivo');
  await expect(dialog).toContainText('Nombre del dispositivo');
  await expect(dialog).toContainText('Asignar a unidad');
  await expect(dialog.getByRole('button', { name: 'Cancelar' })).toBeVisible();
  await expect(dialog).not.toContainText(/Add Device|Device Type|Device Name|Assign to Unit|Select a|Cancel\b/);

  await dialog.getByRole('button', { name: 'Agregar Dispositivo' }).click();
  await expect(dialog).toContainText('Selecciona un tipo de dispositivo.');
  await expect(dialog).toContainText('El nombre del dispositivo debe tener entre 2 y 50 caracteres.');
});
