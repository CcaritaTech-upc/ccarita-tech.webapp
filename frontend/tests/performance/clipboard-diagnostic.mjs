import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { writeFileSync } from 'node:fs';
const root = resolve(process.argv[2]);
const { chromium } = await import(pathToFileURL(join(root, 'node_modules/playwright/index.mjs')).href);
const browser = await chromium.launch({ executablePath: 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
const page = await browser.newPage();
let errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', e => errors.push(e.message));
try {
  await page.goto('http://127.0.0.1:18081/iam/register-builder');
  await page.locator('#email').waitFor();
  const age = page.locator('#age input');
  const results = [];
  for (const withData of [true, false]) {
    errors = [];
    const result = await age.evaluate((input, withData) => {
      const data = new DataTransfer(); data.setData('text', '30');
      const event = new ClipboardEvent('paste', { cancelable: true, ...(withData ? { clipboardData: data } : {}) });
      input.dispatchEvent(event);
      return { clipboardDataPresent: !!event.clipboardData, defaultPrevented: event.defaultPrevented, value: input.value, visible: input.getBoundingClientRect().height > 0 };
    }, withData);
    await page.waitForTimeout(100);
    results.push({ ...result, errors: [...errors] });
  }
  writeFileSync(join(root, 'clipboard-diagnostic.json'), JSON.stringify(results, null, 2));
  console.log(JSON.stringify(results, null, 2));
} finally { await browser.close(); }
