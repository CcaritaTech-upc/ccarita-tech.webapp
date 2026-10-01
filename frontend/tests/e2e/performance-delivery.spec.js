import { test, expect } from '@playwright/test';

test('Nginx static delivery: compression, scoped immutable cache, fresh SPA and robots', async ({ request }) => {
  test.skip(process.env.E2E_NGINX !== '1', 'Requires the production Nginx delivery boundary');
  const html = await request.get('/iam/login');
  expect(html.status()).toBe(200);
  expect(html.headers()['cache-control']).toBe('no-cache');
  const body = await html.text();
  for (const extension of ['js', 'css']) {
    const asset = body.match(new RegExp(`/assets/[^" ]+\\.${extension}`))?.[0];
    expect(asset, `built ${extension} asset in HTML`).toBeTruthy();
    const compressed = await request.get(asset, { headers: { 'Accept-Encoding': 'gzip' } });
    expect(compressed.status()).toBe(200);
    expect(compressed.headers()['content-encoding']).toBe('gzip');
    expect(compressed.headers().vary).toMatch(/Accept-Encoding/i);
    expect(compressed.headers()['cache-control']).toBe('public, max-age=31536000, immutable');
    const identity = await request.get(asset, { headers: { 'Accept-Encoding': 'identity' } });
    expect(identity.headers()['content-encoding']).toBeUndefined();
    expect(await compressed.body()).toEqual(await identity.body());
  }
  const index = await request.get('/index.html');
  expect(index.headers()['cache-control']).toBe('no-cache');
  expect(await index.text()).toBe(body);
  const missing = await request.get('/assets/missing-12345678.js');
  expect(missing.status()).toBe(404);
  expect(missing.headers()['cache-control']).toBeUndefined();
  const unversioned = await request.get('/vite.svg');
  expect(unversioned.status()).toBe(200);
  expect(unversioned.headers()['cache-control']).toBe('no-cache');
  const api = await request.get('/api/v1/users');
  expect(api.status()).toBe(401);
  expect(api.headers()['cache-control'] || '').not.toMatch(/immutable|public|max-age/);
  const robots = await request.get('/robots.txt');
  expect(robots.status()).toBe(200);
  expect(robots.headers()['content-type']).toContain('text/plain');
  expect(await robots.text()).toBe('User-agent: *\nAllow: /\n');
  expect(body).toContain('name="description"');
});

for (const route of ['login', 'register-owner', 'register-builder']) {
  test(`Auth ${route} does not request chart, Stripe or Cloudinary SDKs`, async ({ page }) => {
    const requests = [];
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('request', request => requests.push(request.url()));
    await page.goto(`/iam/${route}`);
    expect(errors, 'auth entry must initialize before interaction').toEqual([]);
    await expect(page.locator('#email')).toBeVisible();
    await page.waitForLoadState('networkidle');
    expect(errors, 'auth modules must mount without runtime exceptions').toEqual([]);
    expect(requests.filter(url => /\/charts-[^/]+\.js|\/stripe-[^/]+\.js|js\.stripe\.com|widget\.cloudinary\.com|cdnjs.*Chart/i.test(url))).toEqual([]);
    const hero = page.locator('link[rel="preload"][as="image"]');
    await expect(hero).toHaveCount(1);
    await expect(hero).toHaveAttribute('fetchpriority', 'high');
    const url = await hero.getAttribute('href');
    const background = await page.locator('.auth-image-side').evaluate(element => getComputedStyle(element).backgroundImage);
    expect(background).toBe(`url("${url}")`);
    expect(requests.filter(request => request === url)).toHaveLength(1);
  });
}

test('Authenticated non-auth entry does not request the auth background', async ({ page }) => {
  const email = `e2e.preload.${Date.now()}@example.test`;
  const registration = await page.request.post('/api/v1/users', {
    data: { email, password: 'secret123', role: 'Builder' },
  });
  expect(registration.status()).toBe(201);
  const session = await page.request.post('/api/v1/sessions', {
    data: { email, password: 'secret123' },
  });
  expect(session.status()).toBe(201);
  const user = await session.json();
  await page.addInitScript(user => {
    localStorage.setItem('token', user.token);
    localStorage.setItem('currentUser', JSON.stringify(user));
  }, user);
  const images = [];
  page.on('request', request => { if (request.url().includes('img.freepik.com')) images.push(request.url()); });
  await page.goto('/projects');
  await expect(page).toHaveURL(/\/projects$/);
  await expect(page.locator('.logout-button')).toBeVisible();
  await page.waitForLoadState('networkidle');
  await expect(page.locator('link[rel="preload"][as="image"]')).toHaveCount(0);
  expect(images).toEqual([]);
});

for (const actor of ['builder', 'owner']) {
  for (const outcome of ['success', 'failure']) {
    test(`Registration ${actor}: first photo action after deferred SDK ${outcome}`, async ({ page }) => {
      test.skip(process.env.E2E_CLOUDINARY_DUMMY !== '1', 'Requires a frontend built with local-performance-dummy cloud name and upload preset');
      let loads = 0;
      await page.route('**/api/v1/authentication/invitation?**', route => route.fulfill({
        json: { assigned: true, unitId: 1, alreadyRegistered: false },
      }));
      await page.route('https://widget.cloudinary.com/**', route => {
        loads++;
        return outcome === 'failure' ? route.abort('failed') : route.fulfill({
          contentType: 'application/javascript',
          body: `window.cloudinary = { openUploadWidget(config, callback) {
            window.__uploadConfig = config;
            callback(null, {event:'success', info:{secure_url:'/vite.svg'}});
          }};`,
        });
      });
      await page.goto(`/iam/register-${actor}`);
      await page.locator('#email').fill('lazy-upload@example.test');
      await page.locator('#password').fill('secret123');
      await page.locator('#confirmPassword').fill('secret123');
      await page.getByRole('button', { name: /^next$/i }).click();
      await expect(page.locator('#name')).toBeVisible();
      expect(loads).toBe(0);
      const upload = page.locator('button').filter({ has: page.locator('.pi-cloud-upload') });
      if (outcome === 'success') {
        await upload.click();
        await expect(page.locator('.uploaded-image')).toHaveAttribute('src', '/vite.svg');
        expect(loads).toBe(1);
        expect(await page.evaluate(() => ({
          cloud: window.__uploadConfig.cloud_name,
          preset: window.__uploadConfig.upload_preset,
          cropping: window.__uploadConfig.cropping,
        }))).toEqual({ cloud: 'local-performance-dummy', preset: 'local-performance-dummy', cropping: true });
        await upload.click();
        expect(loads).toBe(1);
      } else {
        const chooser = page.waitForEvent('filechooser');
        await upload.click();
        await (await chooser).setFiles({ name: 'avatar.svg', mimeType: 'image/svg+xml', buffer: Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>') });
        await expect(page.locator('.uploaded-image')).toHaveAttribute('src', /^data:image\/svg\+xml;base64,/);
        expect(loads).toBe(1);
      }
    });
  }
}
