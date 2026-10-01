import { test, expect } from '@playwright/test';

async function chooseLanguage(page, language) {
  await page.locator('.language-select').click();
  await page.getByRole('option', { name: language, exact: true }).click();
}

test('Builder UI translates client and plan journeys and keeps sidebar logout legible', async ({ page }) => {
  const suffix = `${Date.now()}${Math.random().toString(36).slice(2, 7)}`;
  const email = `ui.builder.${suffix}@example.test`;
  const password = 'ui-builder-password-123';

  const registration = await page.request.post('/api/v1/users', {
    data: { email, password, role: 'Builder' },
  });
  expect(registration.status()).toBe(201);

  const login = await page.request.post('/api/v1/sessions', { data: { email, password } });
  expect(login.status()).toBe(201);
  const builder = await login.json();
  const headers = { Authorization: `Bearer ${builder.token}` };

  const subscription = await page.request.post('/api/v1/subscriptions', {
    headers,
    data: { builderId: builder.id, planId: 1, startDate: new Date().toISOString(), endDate: null },
  });
  expect(subscription.status()).toBe(201);

  const projectResponse = await page.request.post('/api/v1/projects', {
    headers,
    data: { name: `UI Project ${suffix}`, description: 'Localization E2E', location: 'E2E', totalUnits: 1, builderId: builder.id, imageUrl: null },
  });
  expect(projectResponse.status()).toBe(201);
  const project = await projectResponse.json();

  const unitResponse = await page.request.post('/api/v1/units', {
    headers,
    data: { projectId: project.id, unitNumber: 'UI-1', floor: 1, roomNumber: 'UI-1' },
  });
  expect(unitResponse.status()).toBe(201);
  const unit = await unitResponse.json();

  const clientResponse = await page.request.post('/api/v1/clients', {
    headers,
    data: {
      fullName: 'UI Localization Client',
      projectName: project.name,
      accountStatement: 'Active',
      builderId: builder.id,
      projectId: project.id,
      email: `ui.client.${suffix}@example.test`,
      phoneNumber: '+51987654321',
      address: 'E2E address',
      unitId: unit.id,
      unitNumber: unit.unitNumber,
    },
  });
  expect(clientResponse.status()).toBe(201);
  const client = await clientResponse.json();

  await page.goto('/iam/login');
  await page.evaluate(user => {
    localStorage.setItem('token', user.token);
    localStorage.setItem('currentUser', JSON.stringify({ id: user.id, email: user.email, role: user.role }));
  }, builder);

  await page.goto('/clients');
  await expect(page.getByText('UI Localization Client')).toBeVisible();
  await expect(page.locator('.clients-table .p-paginator-current')).toHaveText('1 to 1 of 1');

  await page.getByRole('button', { name: 'Add Client' }).click();
  const englishDialog = page.locator('.client-add-dialog');
  await expect(englishDialog).toContainText('Add a client');
  await expect(englishDialog).toContainText('Assigned Unit / Apartment');
  await page.getByRole('button', { name: 'Cancel' }).click();

  await chooseLanguage(page, 'ES');
  await expect(page.locator('.clients-table .p-paginator-current')).toHaveText('1 a 1 de 1');
  await expect(page.getByRole('button', { name: 'Agregar Cliente' })).toBeVisible();
  await page.getByRole('button', { name: 'Agregar Cliente' }).click();
  const spanishDialog = page.locator('.client-add-dialog');
  await expect(spanishDialog).toContainText('Agregar un cliente');
  await expect(spanishDialog).toContainText('Unidad / Departamento asignado');
  await page.getByRole('button', { name: 'Cancelar' }).click();

  await page.getByRole('button', { name: 'Ver Perfil' }).click();
  await expect(page).toHaveURL(new RegExp(`/clients/${client.id}$`));
  await expect(page.locator('.client-profile-card').getByText('0 dispositivos vinculados').first()).toBeVisible();
  await page.getByRole('button', { name: 'Editar Cliente' }).click();
  const spanishEditDialog = page.locator('.client-edit-dialog');
  await expect(spanishEditDialog).toContainText('Estado de Cuenta');
  await page.getByRole('button', { name: 'Cancelar' }).click();

  await page.goto('/subscriptions/my-subscription');
  await expect(page.locator('.page-subtitle')).toHaveText('Administra el plan de tu empresa, supervisa las cuotas de dispositivos IoT y descarga tus comprobantes de facturación.');
  await expect(page.locator('.plan-card').filter({ hasText: 'Starter' }).getByText('Perfecto para proyectos pequeños')).toBeVisible();
  await expect(page.locator('.plan-card').filter({ hasText: 'Starter' }).getByText('Hasta 50 dispositivos IoT')).toBeVisible();
  await expect(page.locator('.hero-plan-desc')).toHaveText('Perfecto para proyectos pequeños');
  await expect(page.locator('.submeta-val')).toContainText('proyectos activos');
  await page.getByRole('button', { name: 'Comparar todos los planes' }).first().click();
  const spanishComparison = page.locator('.comparison-table');
  await expect(spanishComparison).toContainText('Dispositivos IoT compatibles');
  await expect(spanishComparison).toContainText('Hasta 50 dispositivos');
  await page.getByRole('button', { name: 'Cerrar', exact: true }).click();

  await chooseLanguage(page, 'EN');
  await expect(page.locator('.plan-card').filter({ hasText: 'Starter' }).getByText('Perfect for small projects')).toBeVisible();
  await expect(page.locator('.plan-card').filter({ hasText: 'Starter' }).getByText('Up to 50 IoT devices')).toBeVisible();
  await expect(page.locator('.hero-plan-desc')).toHaveText('Perfect for small projects');
  await expect(page.locator('.page-subtitle')).toHaveText("Manage your company's plan, monitor IoT device quotas, and download billing receipts.");
  await page.getByRole('button', { name: 'Compare all plans' }).first().click();
  const englishComparison = page.locator('.comparison-table');
  await expect(englishComparison).toContainText('Supported IoT devices');
  await expect(englishComparison).toContainText('Up to 50 devices');
  await page.locator('.p-dialog-content .btn-close').click();

  await page.getByRole('button', { name: 'Open navigation' }).click();
  const sidebarLogout = page.locator('.sidebar-content .logout-item');
  await expect(sidebarLogout).toBeVisible();
  expect(await sidebarLogout.evaluate(element => getComputedStyle(element).color)).toBe('rgb(17, 24, 39)');
  expect(await page.locator('.sidebar-content').evaluate(element => getComputedStyle(element).backgroundColor)).toBe('rgb(16, 185, 129)');
  await sidebarLogout.click();
  await expect(page).toHaveURL(/\/iam\/login/);
});
