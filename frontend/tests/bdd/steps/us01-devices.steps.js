import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { createTestProject, defineTestProjectStructure, registerTestActor } from '../support/app-fixtures.js';

async function createBuilderProject(world, alias, defineStructure) {
  world.actor = await registerTestActor(world.page, 'Builder', alias);
  world.project = await createTestProject(world.page, {
    name: `E2E Device Tower ${alias}`,
    description: `Device dashboard scenario ${alias}`,
    location: 'Av. Primavera 123, Lima',
    totalUnits: 1,
  });
  if (defineStructure) await defineTestProjectStructure(world.page, world.project.id);
}

Given('el constructor con alias {word} tiene un proyecto con estructura de un piso y una unidad', async function (alias) {
  await createBuilderProject(this, alias, true);
});

Given('el constructor con alias {word} tiene un proyecto sin estructura', async function (alias) {
  await createBuilderProject(this, alias, false);
});

When('abre el dashboard del constructor', async function () {
  await this.page.goto('/analytics/dashboard');
  await expect(this.page.locator('.builder-dashboard')).toBeVisible({ timeout: 20_000 });
});

Then('la distribución mostrará los cinco tipos de dispositivos de la estructura', async function () {
  await expect(this.page.locator('.devices-grid .device-type-card')).toHaveCount(5);
});

When('abre la administración de dispositivos', async function () {
  await this.page.goto('/devices/device-management');
});

Then('la tabla mostrará los dispositivos de piso y unidad con su estado de conexión', async function () {
  const expectedNames = [
    'Smart Meter - Floor 1',
    'Water Sensor - Floor 1',
    'Smoke Detector - Floor 1',
    'Air Conditioner - Unit 101',
    'Smart Light - Unit 101',
  ];
  for (const name of expectedNames) {
    const row = this.page.getByRole('row').filter({ hasText: name }).first();
    await expect(row).toBeVisible();
    await expect(row).toContainText(/Online|Offline/);
  }
});

Then('la distribución no tendrá tarjetas de tipos de dispositivos', async function () {
  await expect(this.page.locator('.devices-grid .device-type-card')).toHaveCount(0);
});
