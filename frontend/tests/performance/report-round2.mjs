import { readFileSync, readdirSync, writeFileSync, existsSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';
const root = resolve(process.argv[2]);
const before = JSON.parse(readFileSync(join(root, 'summary-before.json'), 'utf8'));
const originalBaselineNames = new Set(before.map(r => r.name));
if (existsSync(join(root, 'summary-before-noise.json'))) {
  for (const row of JSON.parse(readFileSync(join(root, 'summary-before-noise.json'), 'utf8'))) if (!before.some(b => b.name === row.name && b.profile === row.profile)) before.push(row);
}
if (process.argv.includes('--inspect')) {
  for (const name of ['register-builder', 'profile-builder', 'profile-owner', 'analytics-builder', 'devices-owner']) {
    const report = JSON.parse(readFileSync(join(root, `perf-surgical-round2-${process.env.PERF_INSPECT_LABEL || 'before'}-${name}-mobile.report.json`), 'utf8'));
    for (const id of ['color-contrast', 'aria-required-children', 'label', 'button-name', 'select-name']) console.log(name, id, JSON.stringify(report.audits[id]?.details?.items?.map(i => ({selector:i.node?.selector, explanation:i.node?.explanation}))));
  }
} else {
  const after = JSON.parse(readFileSync(join(root, `summary-${process.env.PERF_AFTER_LABEL || 'after-final'}.json`), 'utf8'));
  if (existsSync(join(root, 'summary-after-stepper.json'))) {
    for (const row of JSON.parse(readFileSync(join(root, 'summary-after-stepper.json'), 'utf8'))) {
      const index = after.findIndex(a => a.name === row.name && a.profile === row.profile);
      if (index >= 0) after[index] = row;
    }
  }
  const comparisons = before.map(b => {
    const a = after.find(a => a.name === b.name && a.profile === b.profile);
    if (!b.valid || !a?.valid) throw new Error('Missing valid pair');
    console.log(`| ${b.name} | ${b.profile} | ${b.scores.performance} → ${a.scores.performance} | ${b.scores.accessibility} → ${a.scores.accessibility} | ${(b.metrics['largest-contentful-paint']/1000).toFixed(2)} → ${(a.metrics['largest-contentful-paint']/1000).toFixed(2)} | ${Math.round(b.metrics['total-blocking-time'])} → ${Math.round(a.metrics['total-blocking-time'])} |`);
    return { name: b.name, profile: b.profile, before: b, after: a };
  });
  const files = readdirSync(root).filter(f => /\.report\.(json|html)$/.test(f));
  for (const f of files) if (/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+|Bearer\s+(?!\[REDACTED\])[^\s"<>]+/i.test(readFileSync(join(root, f), 'utf8'))) throw new Error('Sensitive artifact ' + f);
  for (const file of readdirSync(root).filter(f => /^seed-summary.*\.json$/.test(f))) {
    const clean = readFileSync(join(root, file), 'utf8').replace(/\/payments\/sessions\/cs_sim_[^"\s]+/g, '/payments/sessions/[REDACTED]');
    writeFileSync(join(root, file), clean);
  }
  for (const dir of readdirSync(root).filter(f => /^chrome-(before|after)(-|$)/.test(f))) rmSync(join(root, dir), { recursive: true, force: true });
  const diagnostics = { runtimeErrors: after.filter(r => r.runtimeError), warnings: after.filter(r => r.runWarnings.length), failedRequests: after.filter(r => r.failures.length), consoleErrorScreens: after.filter(r => r.errors.length).map(r => ({ name: r.name, profile: r.profile, errors: r.errors })) };
  console.log('FINAL DIAGNOSTICS', JSON.stringify(diagnostics));
  writeFileSync(join(root, 'comparison.json'), JSON.stringify({ comparisons, sanitizedReports: files.length, diagnostics }, null, 2));
  if (existsSync(join(root, 'summary-before-noise.json'))) {
    const noise = JSON.parse(readFileSync(join(root, 'summary-before-noise.json'), 'utf8'));
    for (const n of noise) {
      const b = before.find(b => b.name === n.name && b.profile === n.profile);
      if (b && originalBaselineNames.has(n.name)) console.log('BASELINE REPEAT', n.name, n.profile, JSON.stringify({ performance: [b.scores.performance, n.scores.performance], tbt: [Math.round(b.metrics['total-blocking-time']), Math.round(n.metrics['total-blocking-time'])], lcp: [b.metrics['largest-contentful-paint'], n.metrics['largest-contentful-paint']] }));
    }
  }
}
