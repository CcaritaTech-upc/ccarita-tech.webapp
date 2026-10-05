import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { clearBrowserSession, registerTestActor } from '../support/app-fixtures.js';

Given('el usuario de prueba con rol {word} y alias {word} tiene el nombre {string} y teléfono {string}', async function (role, alias, name, phoneNumber) {
  this.actor = await registerTestActor(this.page, role, alias, { name, phoneNumber });
  await this.page.goto('/analytics/dashboard');
});

When('abre su perfil desde el enlace de usuario', async function () {
  await this.page.locator('.user-info-link').click();
  await expect(this.page).toHaveURL(/\/profiles\/profile$/);
});

Then('el perfil mostrará nombre {string}, correo de su cuenta y teléfono {string}', async function (name, phoneNumber) {
  const rolePrefix = this.actor.role;
  await expect(this.page.locator('.profile-name').first()).toHaveText(name);
  await expect(this.page.locator(`#${rolePrefix}-profile-email`)).toHaveValue(this.actor.email);
  await expect(this.page.locator(`#${rolePrefix}-profile-phone`)).toHaveValue(phoneNumber);
});

Then('se mostrará el rol asignado {string}', async function (roleLabel) {
  await expect(this.page.locator('.profile-role')).toHaveText(roleLabel);
});

Given('un visitante sin sesión iniciada', async function () {
  await clearBrowserSession(this.page);
});

When('navega a la ruta protegida {string}', async function (path) {
  await this.page.goto(path);
});

Then('la aplicación redirigirá al usuario a {string}', async function (expectedPath) {
  await expect.poll(() => new URL(this.page.url()).pathname).toBe(expectedPath);
});

Then('mostrará la pantalla de inicio de sesión', async function () {
  await expect(this.page.locator('#email')).toBeVisible();
  await expect(this.page.locator('#password')).toBeVisible();
});
