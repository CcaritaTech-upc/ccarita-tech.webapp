import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { createTestProject, defineTestProjectStructure, registerTestActor } from '../support/app-fixtures.js';

Given('el constructor con alias {word} tiene el proyecto {string} en {string} con estructura de un piso y una unidad', async function (alias, name, location) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
  this.project = await createTestProject(this.page, {
    name,
    description: `Project ${alias} details acceptance test`,
    location,
    totalUnits: 1,
  });
  await defineTestProjectStructure(this.page, this.project.id);
});

When('abre los detalles del proyecto', async function () {
  await this.page.goto(`/projects/${this.project.id}`);
  await expect(this.page.getByRole('heading', { name: this.project.name, exact: true })).toBeVisible();
});

Then('la vista mostrará {string} y la ubicación {string}', async function (name, location) {
  await expect(this.page.getByRole('heading', { name, exact: true })).toBeVisible();
  await expect(this.page.locator('dd').filter({ hasText: location })).toBeVisible();
});

Then('mostrará la unidad creada', async function () {
  await expect(this.page.locator('.unit-card')).toHaveCount(1);
});

Given('el constructor con alias {word} tiene una sesión activa', async function (alias) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
});

When('solicita por API los detalles del proyecto inexistente {int}', async function (projectId) {
  const token = await this.page.evaluate(() => localStorage.getItem('token'));
  this.projectResponse = await this.page.request.get(`/api/v1/projects/${projectId}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
});

Then('la API de proyectos responderá con código {int} Not Found', async function (statusCode) {
  expect(this.projectResponse.status()).toBe(statusCode);
});
