import { writeFileSync, readFileSync, rmSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';

const root = resolve(process.argv[2]);
const phase = process.argv[3];
if (!['before', 'after'].includes(phase)) throw new Error('Use before or after');
const label = phase + (process.env.PERF_RUN_LABEL ? `-${process.env.PERF_RUN_LABEL}` : '');
const dependency = async path => import(pathToFileURL(join(root, 'node_modules', path)).href);
const { chromium } = await dependency('playwright/index.mjs');
const { default: lighthouse } = await dependency('lighthouse/core/index.js');
const { default: desktopConfig } = await dependency('lighthouse/core/config/desktop-config.js');
const { default: puppeteer } = await dependency('puppeteer-core/lib/esm/puppeteer/puppeteer-core.js');
const origin = 'http://127.0.0.1:18081';
let ready = false;
for (let attempt = 0; attempt < 60; attempt++) {
  try { ready = (await fetch(origin + '/health')).ok; } catch { /* Stack startup is retried only before any fixture mutation. */ }
  if (ready) break;
  await new Promise(resolve => setTimeout(resolve, 1000));
}
if (!ready) throw new Error('Isolated API migration readiness did not become healthy');
const password = 'secret123';
const sessions = {}, calls = [];
function sanitize(text) {
  return String(text).replace(/Bearer\s+[^\s"<>]+/gi, 'Bearer [REDACTED]').replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '[REDACTED_JWT]').replace(/([?&](?:token|session_id|key|secret|password)=)[^&\s"<>]+/gi, '$1[REDACTED]');
}
async function api(path, data, actor, method = 'POST') {
  const response = await fetch(origin + '/api/v1' + path, { method, headers: { 'Content-Type': 'application/json', ...(actor ? { Authorization: `Bearer ${actor.token}` } : {}) }, ...(data ? { body: JSON.stringify(data) } : {}) });
  calls.push({ path: path.replace(/\/payments\/sessions\/[^/]+/g, '/payments/sessions/[REDACTED]'), method, status: response.status });
  if (!response.ok) throw new Error(`${method} ${path}: HTTP ${response.status}`);
  const text = await response.text(); return text ? JSON.parse(text) : null;
}
async function signIn(role) { return api('/sessions', { email: `perf.private.${role.toLowerCase()}@example.test`, password }); }
let catalog;
if ((phase === 'before' && !process.argv.includes('--resume')) || process.argv.includes('--seed')) {
  for (const role of ['Builder']) {
    await api('/users', { email: 'perf.private.builder@example.test', password, role }); sessions[role] = await signIn(role);
    await api('/profiles', { userId: sessions[role].id, name: `Perf ${role}`, username: 'perfbuilder', address: 'Av. Local Fixture 100', age: 30, phoneNumber: '+51987654310', photoUrl: '', secondEmail: '' }, sessions[role]);
  }
  const builder = sessions.Builder;
  const plans = await api('/plans', null, builder, 'GET');
  const checkout = await api('/subscriptions/payments/sessions', { builderId: builder.id, planId: plans[0].id, successUrl: origin + '/subscriptions/my-subscription?success=true', cancelUrl: origin + '/subscriptions/my-subscription?canceled=true' }, builder);
  await api(`/subscriptions/payments/sessions/${checkout.sessionId}`, { builderId: builder.id, status: 'confirmed' }, builder, 'PATCH');
  const project = await api('/projects', { name: 'Perf Local Towers', description: 'Small representative fixture', location: 'Lima local fixture', totalUnits: 3, builderId: builder.id, imageUrl: null }, builder);
  const units = [];
  for (let i = 1; i <= 3; i++) units.push(await api('/units', { projectId: project.id, unitNumber: `P${i}`, floor: i < 3 ? 1 : 2, roomNumber: `P${i}` }, builder));
  const client = await api('/clients', { fullName: 'Perf Owner', projectName: project.name, accountStatement: 'Active', builderId: builder.id, projectId: project.id, email: 'perf.private.owner@example.test', phoneNumber: '+51987654311', address: 'Av. Local Fixture 101', unitId: units[0].id, unitNumber: units[0].unitNumber }, builder);
  await api('/clients', { fullName: 'Perf Pending Client', projectName: project.name, accountStatement: 'Stand by', builderId: builder.id, projectId: project.id, email: 'perf.private.pending@example.test', phoneNumber: '+51987654312', address: 'Av. Local Fixture 102', unitId: null, unitNumber: '' }, builder);
  await api('/users', { email: 'perf.private.owner@example.test', password, role: 'Owner' }); sessions.Owner = await signIn('Owner');
  await api('/profiles', { userId: sessions.Owner.id, name: 'Perf Owner', username: 'perfowner', address: 'Av. Local Fixture 100', age: 30, phoneNumber: '+51987654310', photoUrl: '', secondEmail: '' }, sessions.Owner);
  await api('/devices', { name: 'Perf Hall Light', type: 'SmartLight', location: 'Hall', projectId: project.id, unitId: units[0].id, status: 'online' }, sessions.Owner);
  catalog = [
    ['Anonymous', 'register-builder', '/iam/register-builder', '.auth-container #email', null],
    ['Builder', 'analytics-builder', '/analytics/dashboard', '.builder-dashboard .stats-grid', null],
    ['Owner', 'analytics-owner', '/analytics/dashboard', '.owner-dashboard .stats-grid', null],
    ['Builder', 'projects-list', '/projects', '.project-grid', 'Perf Local Towers'],
    ['Builder', 'projects-create', '/projects/new', '.form-container .form-card', null],
    ['Builder', 'projects-detail', `/projects/${project.id}`, '.project-details-root .structure-floors', 'Perf Local Towers'],
    ['Builder', 'clients-list', '/clients', '.p-datatable', 'Perf Owner'],
    ['Builder', 'clients-detail', `/clients/${client.id}`, '.client-profile-card h1', 'Perf Owner'],
    ['Builder', 'profile-builder', '/profiles/profile', '.profile-name', 'Perf Builder'],
    ['Owner', 'profile-owner', '/profiles/profile', '.profile-name', 'Perf Owner'],
    ['Builder', 'devices-builder', '/devices/device-management', '.device-management-view .p-datatable', 'Perf Hall Light'],
    ['Owner', 'devices-owner', '/devices/device-management', '.unit-devices-table', 'Perf Hall Light'],
    ['Builder', 'my-subscription', '/subscriptions/my-subscription', '.hero-plan-title', plans[0].name],
  ];
  writeFileSync(join(root, 'catalog.json'), JSON.stringify(catalog, null, 2));
  const registry = await api('/devices', null, builder, 'GET');
  writeFileSync(join(root, `seed-summary-${phase}.json`), JSON.stringify({ calls, accounts: 2, profiles: 2, activeSubscription: 1, projects: 1, units: 3, floors: 2, clients: 2, customDevices: 1, registryDevices: registry.length, liveTelemetry: false }, null, 2));
} else {
  catalog = JSON.parse(readFileSync(join(root, 'catalog.json'), 'utf8'));
  for (const role of ['Builder', 'Owner']) sessions[role] = await signIn(role);
}
catalog.push(['Anonymous', 'login', '/iam/login', '.auth-container #email', null], ['Anonymous', 'register-owner', '/iam/register-owner', '.auth-container #email', null]);
const context = await chromium.launchPersistentContext(join(root, `chrome-${label}`), { executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true, args: ['--remote-debugging-port=19229', '--disable-gpu', '--no-sandbox'] });
let errors = [], failures = [];
function observe(page) {
  page.on('console', m => { if (m.type() === 'error') errors.push(sanitize(m.text())); });
  page.on('pageerror', e => errors.push(sanitize(e.message)));
  page.on('response', r => { if (r.status() >= 400) failures.push({ url: sanitize(r.url()), status: r.status() }); });
  page.on('requestfailed', r => failures.push({ url: sanitize(r.url()), error: sanitize(r.failure()?.errorText) }));
}
context.pages().forEach(observe); context.on('page', observe);
const setupPage = context.pages()[0];
const browser = await puppeteer.connect({ browserURL: 'http://127.0.0.1:19229' });
const [page, target] = await Promise.all([context.waitForEvent('page'), browser.newPage()]);
const results = process.argv.includes('--resume') ? JSON.parse(readFileSync(join(root, `summary-${label}.json`), 'utf8')) : [];
async function validate(row, currentPage) {
  await currentPage.locator(row[3]).first().waitFor({ state: 'visible', timeout: 20000 });
  if (new URL(currentPage.url()).pathname !== row[2]) throw new Error('Unexpected final URL');
  if (row[4]) await currentPage.getByText(row[4], { exact: false }).first().waitFor({ state: 'visible', timeout: 20000 });
  return { url: currentPage.url(), selector: row[3], fixtureText: row[4], visible: true };
}
try {
  for (const row of catalog) {
    if (process.env.PERF_SCREENS && !process.env.PERF_SCREENS.split(',').includes(row[1])) continue;
    await setupPage.goto(origin + '/iam/register-builder');
    await setupPage.evaluate(actor => { localStorage.clear(); if (actor) { localStorage.setItem('token', actor.token); localStorage.setItem('currentUser', JSON.stringify({ id: actor.id, email: actor.email, role: actor.role })); } }, sessions[row[0]] || null);
    await setupPage.goto(origin + row[2]); await validate(row, setupPage);
    for (const profile of ['mobile', 'desktop']) {
      if (results.some(r => r.name === row[1] && r.profile === profile && r.valid)) continue;
      await setupPage.goto('about:blank');
      const cdp = await context.newCDPSession(setupPage); await cdp.send('Network.enable'); await cdp.send('Network.clearBrowserCache'); await cdp.detach();
      errors = []; failures = [];
      try {
        const { lhr, report } = await lighthouse(origin + row[2], { port: 19229, disableStorageReset: true, logLevel: 'error', output: ['json', 'html'], onlyCategories: ['performance', 'accessibility', 'best-practices', 'seo'] }, profile === 'desktop' ? desktopConfig : undefined, target);
        const rendered = await validate(row, page);
        if (new URL(lhr.finalDisplayedUrl || lhr.finalUrl).pathname !== row[2] || lhr.runtimeError) throw new Error('Invalid Lighthouse navigation');
        const scores = Object.fromEntries(Object.entries(lhr.categories).map(([k, v]) => [k, Math.round(v.score * 100)]));
        const metrics = Object.fromEntries(['first-contentful-paint', 'largest-contentful-paint', 'total-blocking-time', 'cumulative-layout-shift', 'total-byte-weight'].map(k => [k, lhr.audits[k]?.numericValue]));
        for (const [extension, content] of [['json', JSON.stringify(lhr)], ['html', report[1]]]) {
          const clean = sanitize(content);
          if (/"(?:authorization|token|password)"\s*:/i.test(clean) || Object.values(sessions).some(s => clean.includes(s.token))) throw new Error('Sensitive report data');
          writeFileSync(join(root, `perf-surgical-round2-${label}-${row[1]}-${profile}.report.${extension}`), clean);
        }
        results.push({ name: row[1], role: row[0], route: row[2], phase, profile, valid: true, scores, metrics, rendered, runtimeError: null, runWarnings: lhr.runWarnings, errors: [...new Set(errors)], failures });
      } catch (error) { results.push({ name: row[1], role: row[0], profile, phase, valid: false, error: sanitize(error.message), errors, failures }); }
      writeFileSync(join(root, `summary-${label}.json`), JSON.stringify(results, null, 2));
      console.log(phase, row[1], profile, results.at(-1).scores || results.at(-1).error);
      await target.goto('about:blank');
    }
  }
} finally {
  await setupPage.goto(origin + '/iam/register-builder'); await setupPage.evaluate(() => localStorage.clear());
  browser.disconnect(); await context.close();
  rmSync(join(root, `chrome-${label}`), { recursive: true, force: true });
}
if (results.some(r => !r.valid)) process.exitCode = 1;
