import {
  After,
  AfterAll,
  Before,
  BeforeAll,
  World,
  setDefaultTimeout,
  setWorldConstructor,
} from '@cucumber/cucumber';
import { chromium } from '@playwright/test';
import { clearBrowserSession, createTestProject, registerTestActor } from './app-fixtures.js';

let browser;
let deviceSeedProjectsReserved = false;

setDefaultTimeout(60_000);

class PlaywrightWorld extends World {
  async startBrowserPage() {
    this.context = await browser.newContext({
      baseURL: process.env.E2E_BASE_URL || 'http://127.0.0.1:8081',
    });
    this.page = await this.context.newPage();
  }

  async closeBrowserPage() {
    await this.context?.close();
  }
}

setWorldConstructor(PlaywrightWorld);

BeforeAll(async function () {
  browser = await chromium.launch({ headless: true });
});

Before(async function () {
  await this.startBrowserPage();
  await this.page.goto('/iam/login');
  await this.page.evaluate(() => localStorage.setItem('lang', 'es'));
  await this.page.reload();
});

Before({ tags: '@US01' }, async function () {
  if (deviceSeedProjectsReserved) return;

  const fixtureBuilder = await registerTestActor(this.page, 'Builder', 'device-seed-reservation');
  for (let index = 1; index <= 3; index += 1) {
    await createTestProject(this.page, {
      name: `E2E Device Reservation ${index}`,
      description: `Reserved project for device seed fixture ${index}`,
      location: `Av. Demo ${100 + index}, Lima`,
      totalUnits: 1,
    });
  }

  await clearBrowserSession(this.page);
  await this.page.goto('/iam/login');
  await this.page.evaluate(() => localStorage.setItem('lang', 'es'));
  deviceSeedProjectsReserved = true;
  this.deviceSeedOwner = fixtureBuilder.email;
});

After(async function () {
  await this.closeBrowserPage();
});

AfterAll(async function () {
  await browser?.close();
});
