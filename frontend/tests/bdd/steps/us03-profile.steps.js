import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { registerTestActor } from '../support/app-fixtures.js';

Given('el usuario con rol {word} y alias {word} está editando su perfil', async function (role, alias) {
  this.actor = await registerTestActor(this.page, role, alias, { name: `E2E Profile ${role}` });
  await this.page.goto('/profiles/profile');
  await expect(this.page.locator('.profile-name').first()).toHaveText(this.actor.name, { timeout: 20_000 });
  await this.page.getByRole('button', { name: /edit profile|editar perfil/i }).click();

  this.phoneInput = this.page.locator(`#${this.actor.role}-profile-phone`);
  this.addressInput = this.page.locator(`#${this.actor.role}-profile-address`);
  this.updateRequestCount = 0;
});

When('modifica el teléfono a {string} y la dirección a {string}', async function (phone, address) {
  this.updatedPhone = phone;
  this.updatedAddress = address;
  await this.phoneInput.fill(phone);
  await this.addressInput.fill(address);
});

When('guarda los cambios', async function () {
  const updateResponse = this.page.waitForResponse(
    (response) => response.url().includes('/api/v1/profiles/') && response.request().method() === 'PUT',
  );
  await this.page.locator('.edit-actions .edit-button').click();
  this.updateResponse = await updateResponse;
});

Then('el sistema actualizará los datos satisfactoriamente', async function () {
  expect(this.updateResponse.status()).toBe(200);
  const savedProfile = await this.updateResponse.json();
  expect(savedProfile.phoneNumber).toBe(this.updatedPhone);
  expect(savedProfile.address).toBe(this.updatedAddress);
});

Then('mostrará el mensaje de confirmación {string}', async function (message) {
  await expect(this.page.getByText(message, { exact: true })).toBeVisible();
  await this.page.reload();
  await expect(this.phoneInput).toHaveValue(this.updatedPhone);
  await expect(this.addressInput).toHaveValue(this.updatedAddress);
});

When('ingresa un teléfono inválido {string}', async function (phone) {
  await this.phoneInput.fill(phone);
  this.page.on('request', (request) => {
    if (request.url().includes('/api/v1/profiles/') && request.method() === 'PUT') {
      this.updateRequestCount += 1;
    }
  });
});

When('intenta guardar el perfil', async function () {
  await this.page.locator('.edit-actions .edit-button').click();
});

Then('el sistema bloqueará la actualización', async function () {
  expect(this.updateRequestCount).toBe(0);
  await expect(this.phoneInput).toHaveClass(/input-error/);
});

Then('mostrará la validación de error {string}', async function (message) {
  await expect(this.page.getByText(message, { exact: true })).toBeVisible();
  await expect(this.page.locator('.edit-actions .edit-button')).toBeVisible();
});
