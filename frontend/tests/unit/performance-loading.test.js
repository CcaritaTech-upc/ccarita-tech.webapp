import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { vendorChunk } from '../../vite.config.js';

describe('Vite package boundaries', () => {
  it.each([
    ['primevue/button/index.mjs', 'primevue'],
    ['@primevue/core/basecomponent/index.mjs', 'primevue'],
    ['@primeuix/themes/index.mjs', 'primevue'],
    ['vue/dist/vue.runtime.esm-bundler.js', 'vendor'],
    ['vue-chartjs/dist/index.js', 'charts'],
    ['chart.js/dist/chart.js', 'charts'],
    ['@stripe/stripe-js/lib/pure.js', 'stripe'],
  ])('isolates %s as %s', (path, chunk) => {
    expect(vendorChunk(`/app/node_modules/${path}`)).toBe(chunk);
    expect(vendorChunk(`C:\\app\\node_modules\\${path.replaceAll('/', '\\')}`)).toBe(chunk);
  });
  it('does not hoist application modules', () => {
    expect(vendorChunk('/app/src/primevue-adapter.js')).toBeUndefined();
  });
});

describe('Cloudinary on-demand loading', () => {
  let scripts;
  beforeEach(() => {
    vi.resetModules();
    vi.useFakeTimers();
    scripts = [];
    vi.stubGlobal('window', {});
    vi.stubGlobal('document', {
      createElement: () => ({ remove: vi.fn() }),
      head: { appendChild: script => scripts.push(script) },
    });
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });
  it('does not load on import, shares concurrent requests, resolves the first action', async () => {
    const { loadCloudinaryWidget } = await import('../../src/shared/infrastructure/cloudinary-loader.js');
    expect(scripts).toHaveLength(0);
    const first = loadCloudinaryWidget();
    expect(loadCloudinaryWidget()).toBe(first);
    expect(scripts).toHaveLength(1);
    expect(scripts[0].async).toBe(true);
    window.cloudinary = { openUploadWidget: vi.fn() };
    scripts[0].onload();
    expect(await first).toBe(window.cloudinary);
    expect(await loadCloudinaryWidget()).toBe(window.cloudinary);
    expect(scripts).toHaveLength(1);
  });
  it.each(['error', 'timeout', 'missing-global'])('removes a failed %s load and allows retry', async reason => {
    const { loadCloudinaryWidget } = await import('../../src/shared/infrastructure/cloudinary-loader.js');
    const first = loadCloudinaryWidget();
    const rejected = expect(first).rejects.toThrow('Cloudinary widget could not be loaded');
    if (reason === 'error') scripts[0].onerror();
    if (reason === 'missing-global') scripts[0].onload();
    if (reason === 'timeout') vi.advanceTimersByTime(15000);
    await rejected;
    expect(scripts[0].remove).toHaveBeenCalledOnce();
    const retry = loadCloudinaryWidget();
    expect(scripts).toHaveLength(2);
    window.cloudinary = { openUploadWidget: vi.fn() };
    scripts[1].onload();
    expect(await retry).toBe(window.cloudinary);
  });
});

const { loadStripe } = vi.hoisted(() => ({ loadStripe: vi.fn() }));
vi.mock('@stripe/stripe-js/pure', () => ({ loadStripe }));

describe('Stripe checkout loading', () => {
  beforeEach(() => {
    vi.resetModules();
    loadStripe.mockReset();
    vi.stubEnv('VITE_STRIPE_PUBLISHABLE_KEY', 'pk_test_dummy');
  });
  afterEach(() => vi.unstubAllEnvs());
  it('does not initialize on import and shares the first checkout initialization', async () => {
    const { getStripeClient } = await import('../../src/subscriptions/infrastructure/stripe-client.js');
    expect(loadStripe).not.toHaveBeenCalled();
    const client = { redirectToCheckout: vi.fn() };
    loadStripe.mockResolvedValue(client);
    const first = getStripeClient();
    expect(getStripeClient()).toBe(first);
    expect(await first).toBe(client);
    expect(loadStripe).toHaveBeenCalledExactlyOnceWith('pk_test_dummy');
  });
  it('retries initialization after a rejected load', async () => {
    const { getStripeClient } = await import('../../src/subscriptions/infrastructure/stripe-client.js');
    loadStripe.mockRejectedValueOnce(new Error('network failure')).mockResolvedValueOnce({});
    await expect(getStripeClient()).rejects.toThrow('network failure');
    await expect(getStripeClient()).resolves.toEqual({});
    expect(loadStripe).toHaveBeenCalledTimes(2);
  });
});
