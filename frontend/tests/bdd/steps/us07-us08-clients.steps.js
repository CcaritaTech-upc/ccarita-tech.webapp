import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { createTestClient, createTestProject, registerTestActor } from '../support/app-fixtures.js';

Given('el constructor con alias {word} tiene el cliente {string} en el proyecto {string}', async function (alias, clientName, projectName) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
  this.project = await createTestProject(this.page, {
    name: projectName,
    description: `Client listing project ${alias}`,
    location: 'Av. Primavera 123, Lima',
    totalUnits: 1,
  });
  this.client = await createTestClient(this.page, {
    fullName: clientName,
    email: `bdd.client.${alias}.${Date.now()}@example.test`,
    projectId: this.project.id,
    projectName,
  });
});

Given('el constructor con alias {word} no tiene clientes', async function (alias) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
});

When('abre la sección de clientes', async function () {
  await this.page.goto('/clients');
});

Then('la lista mostrará {string} asociado a {string}', async function (clientName, projectName) {
  const row = this.page.locator('.clients-table tbody tr').filter({ hasText: clientName });
  await expect(row).toBeVisible();
  await expect(row).toContainText(projectName);
});

Then('mostrará el estado de cuenta {string} y la acción de ver perfil', async function (status) {
  const row = this.page.locator('.clients-table tbody tr').first();
  await expect(row).toContainText(status);
  await expect(row.getByRole('button', { name: /ver perfil|view profile/i })).toBeVisible();
});

Then('la lista de clientes estará vacía', async function () {
  await expect(this.page.getByText('No se encontraron clientes', { exact: true })).toBeVisible();
  await expect(this.page.locator('.clients-table tbody tr .font-semibold')).toHaveCount(0);
});

Given('el constructor con alias {word} tiene el proyecto {string}', async function (alias, projectName) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
  this.project = await createTestProject(this.page, {
    name: projectName,
    description: `Client creation project ${alias}`,
    location: 'Av. Primavera 456, Lima',
    totalUnits: 1,
  });
});

When('abre el formulario para agregar un cliente', async function () {
  await this.page.goto('/clients');
  await this.page.locator('.add-client-btn').click();
  await expect(this.page.locator('.client-add-dialog')).toBeVisible();
});

When('ingresa nombre {string} y correo {string}', async function (name, email) {
  this.clientName = name;
  await this.page.locator('#fullName').fill(name);
  await this.page.locator('#email').fill(email);
});

When('selecciona el proyecto {string}', async function (projectName) {
  await this.page.locator('#projectId').click();
  await this.page.getByRole('option', { name: projectName, exact: true }).click();
});

When('registra el cliente', async function () {
  const response = this.page.waitForResponse(
    (request) => request.url().includes('/api/v1/clients') && request.request().method() === 'POST',
  );
  await this.page.locator('.client-add-dialog').getByRole('button', { name: /agregar|add/i }).click();
  this.clientResponse = await response;
});

Then('el API de clientes responderá con código {int} Created', async function (statusCode) {
  expect(this.clientResponse.status()).toBe(statusCode);
});

Then('el cliente {string} aparecerá en la lista con estado {string}', async function (clientName, status) {
  const row = this.page.locator('.clients-table tbody tr').filter({ hasText: clientName });
  await expect(row).toBeVisible();
  await expect(row).toContainText(status);
});

When('ingresa el nombre {string} sin correo', async function (name) {
  this.clientName = name;
  await this.page.locator('#fullName').fill(name);
});

When('intenta registrar el cliente', async function () {
  this.clientRequests = 0;
  this.page.on('request', (request) => {
    if (request.url().includes('/api/v1/clients') && request.request().method() === 'POST') {
      this.clientRequests += 1;
    }
  });
  await this.page.locator('.client-add-dialog').getByRole('button', { name: /agregar|add/i }).click();
});

Then('el formulario bloqueará la creación', async function () {
  expect(this.clientRequests).toBe(0);
  await expect(this.page.locator('.client-add-dialog')).toBeVisible();
});

Then('mostrará la validación {string}', async function (message) {
  await expect(this.page.locator('.client-add-dialog').getByText(message, { exact: true })).toBeVisible();
});
