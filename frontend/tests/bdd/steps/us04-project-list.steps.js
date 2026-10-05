import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { createTestProject, registerTestActor } from '../support/app-fixtures.js';

Given('el constructor con alias {word} tiene el proyecto {string} con {int} unidades', async function (alias, name, totalUnits) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
  this.project = await createTestProject(this.page, {
    name,
    totalUnits: Number(totalUnits),
    description: `Project ${alias} for the US04 scenario`,
    location: 'Av. Primavera 123, Lima',
  });
});

Given('el constructor con alias {word} no tiene proyectos', async function (alias) {
  this.actor = await registerTestActor(this.page, 'Builder', alias);
});

When('abre la lista de proyectos', async function () {
  await this.page.goto('/projects');
});

Then('una tarjeta mostrará {string} y {int} unidades', async function (name, totalUnits) {
  const card = this.page.locator('.project-card-box').filter({ hasText: name });
  await expect(card).toBeVisible();
  await expect(card.locator('.stat-val').first()).toHaveText(String(totalUnits));
});

Then('la tarjeta mostrará un estado', async function () {
  await expect(this.page.locator('.project-card-box .status-tag').first()).toBeVisible();
});

Then('verá el estado vacío de proyectos', async function () {
  await expect(this.page.locator('.empty-state-box')).toBeVisible();
});

Then('tendrá disponible la acción de crear un proyecto', async function () {
  await expect(this.page.locator('.empty-state-box button')).toBeVisible();
});
