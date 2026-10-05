import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { registerTestActor } from '../support/app-fixtures.js';

Given('el constructor con alias {word} abre el formulario de nuevo proyecto', async function (alias) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
  await this.page.goto('/projects/new');
  this.projectRequests = 0;
  this.page.on('request', (request) => {
    if (request.url().includes('/api/v1/projects') && request.method() === 'POST') {
      this.projectRequests += 1;
    }
  });
  this.projectResponse = null;
  this.page.on('response', (response) => {
    if (response.url().includes('/api/v1/projects') && response.request().method() === 'POST') {
      this.projectResponse = response;
    }
  });
});

When('ingresa el nombre {string}, ubicación {string} y descripción {string}', async function (name, location, description) {
  this.projectName = name;
  await this.page.locator('form .form-field input').nth(0).fill(name);
  await this.page.locator('form textarea').fill(description);
  await this.page.locator('form .form-field input').nth(1).fill(location);
});

When('deja vacío el nombre y completa ubicación {string} y descripción {string}', async function (location, description) {
  this.projectName = '';
  await this.page.locator('form textarea').fill(description);
  await this.page.locator('form .form-field input').nth(1).fill(location);
});

When('guarda el proyecto', async function () {
  await this.page.locator('form button[type="submit"]').click();
});

Then('el endpoint de proyectos responderá con código {int} Created', async function (statusCode) {
  await expect.poll(() => this.projectResponse?.status()).toBe(statusCode);
  expect(this.projectResponse.status()).toBe(statusCode);
  this.createdProject = await this.projectResponse.json();
});

Then('el proyecto {string} aparecerá en la lista', async function (name) {
  await this.page.goto('/projects');
  await expect(this.page.locator('.project-card-box').filter({ hasText: name })).toBeVisible();
});

Then('la validación bloqueará la creación del proyecto', async function () {
  expect(this.projectRequests).toBe(0);
  expect(this.projectResponse).toBeNull();
});

Then('mostrará el mensaje {string}', async function (message) {
  await expect(this.page.locator('.p-toast-detail').filter({ hasText: message })).toBeVisible();
});
