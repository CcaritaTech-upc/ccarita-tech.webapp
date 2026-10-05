import { Given, When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';
import { registerTestActor } from '../support/app-fixtures.js';

Given('el constructor con alias {word} tiene una suscripción activa al plan {int}', async function (alias, planId) {
  this.actor = await registerTestActor(this.page, 'Builder', alias, {
    subscriptionPlanId: Number(planId),
  });
});

Given('el constructor con alias {word} no tiene una suscripción activa', async function (alias) {
  this.actor = await registerTestActor(this.page, 'Builder', alias, { activeSubscription: false });
});

When('accede a la sección {string}', async function (_section) {
  await this.page.goto('/subscriptions/my-subscription');
});

When('accede al módulo de suscripciones', async function () {
  await this.page.goto('/subscriptions/my-subscription');
});

Then('verá el plan contratado {string} con costo mensual {string}', async function (planName, price) {
  await expect(this.page.locator('.hero-plan-title')).toHaveText(planName);
  await expect(this.page.locator('.hero-price-amount')).toHaveText(price);
});

Then('el estado de suscripción será {string}', async function (status) {
  await expect(this.page.locator('.status-active')).toContainText(status);
});

Then('verá los planes Starter, Professional y Enterprise disponibles', async function () {
  const plans = this.page.locator('.plan-card');
  await expect(plans).toHaveCount(3);
  await expect(this.page.locator('.plans-grid')).toContainText('Starter');
  await expect(this.page.locator('.plans-grid')).toContainText('Professional');
  await expect(this.page.locator('.plans-grid')).toContainText('Enterprise');
});

Then('podrá seleccionar una opción de checkout por cada plan', async function () {
  const checkoutButtons = this.page.locator('.plan-card .action-btn');
  await expect(checkoutButtons).toHaveCount(3);
  for (const button of await checkoutButtons.all()) {
    await expect(button).toBeEnabled();
  }
});
