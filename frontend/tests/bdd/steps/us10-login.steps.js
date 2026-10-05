import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { clearBrowserSession, registerTestActor, TEST_PASSWORD } from '../support/app-fixtures.js';

Given('el usuario de prueba con rol {word} y alias {word} tiene una cuenta registrada', async function (role, alias) {
  this.actor = await registerTestActor(this.page, role, alias);
});

async function submitLogin(world, password) {
  await clearBrowserSession(world.page);
  await world.page.goto('/iam/login');
  await world.page.locator('#email').fill(world.actor.email);
  await world.page.locator('#password').fill(password);

  const sessionResponse = world.page.waitForResponse((response) => {
    const path = new URL(response.url()).pathname;
    return path.endsWith('/sessions') && response.request().method() === 'POST';
  });
  await world.page.locator('button[type="submit"]').click();
  world.sessionResponse = await sessionResponse;
}

When('inicia sesión con la contraseña correcta {string}', async function (password) {
  expect(password).toBe(TEST_PASSWORD);
  await submitLogin(this, password);
});

Then('la API de sesiones responderá con código {int} Created', async function (statusCode) {
  expect(this.sessionResponse.status()).toBe(statusCode);
});

Then('el usuario tendrá una sesión activa y llegará a {string}', async function (path) {
  await expect.poll(() => new URL(this.page.url()).pathname).toBe(path);
  expect(await this.page.evaluate(() => localStorage.getItem('token'))).toBeTruthy();
});

When('intenta iniciar sesión con la contraseña incorrecta {string}', async function (password) {
  await submitLogin(this, password);
});

Then('la API de sesiones responderá con código {int} Unauthorized', async function (statusCode) {
  expect(this.sessionResponse.status()).toBe(statusCode);
});

Then('el login mostrará el mensaje {string}', async function (message) {
  await expect(this.page.getByText(message, { exact: true })).toBeVisible();
  await expect(this.page).toHaveURL(/\/iam\/login$/);
  expect(await this.page.evaluate(() => localStorage.getItem('token'))).toBeNull();
});
