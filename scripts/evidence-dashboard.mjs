// Evidence dashboard generator — living docs from ledger data, not handwriting.
// Reads docs/bounded-contexts/*/evidence-ledger.md (+ business-rules.md status)
// and emits a single static HTML page. No dependencies, no build step.
// Usage: node scripts/evidence-dashboard.mjs [--out docs/evidence-dashboard.html]
import { readFileSync, writeFileSync, readdirSync, existsSync } from 'node:fs';
import { join, dirname, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const contextsDir = join(root, 'docs', 'bounded-contexts');
const outArg = process.argv.findIndex((a) => a === '--out');
const outRaw = outArg >= 0 ? process.argv[outArg + 1] : join(root, 'docs', 'evidence-dashboard.html');
const outPath = isAbsolute(outRaw) ? outRaw : join(root, outRaw);

// --- Minimal YAML reader: enough for the ledger schema (maps, lists of maps,
// scalars, single/double-quoted strings, nested 2-space indentation). Not a
// general parser by design; fails loudly on unexpected shapes.
function parseScalar(raw) {
  const s = raw.trim();
  if (s === '[]') return [];
  if (s === '{}') return {};
  if (s.startsWith('[') && s.endsWith(']')) {
    // Flow-style list on one line (ledger shorthand for short lists).
    const parts = [];
    let cur = '';
    let quote = null;
    for (const ch of s.slice(1, -1)) {
      if (quote) {
        cur += ch;
        if (ch === quote) quote = null;
      } else if (ch === '"' || ch === "'") {
        quote = ch;
        cur += ch;
      } else if (ch === ',') {
        parts.push(cur);
        cur = '';
      } else {
        cur += ch;
      }
    }
    if (cur.trim() !== '' || parts.length > 0) parts.push(cur);
    return parts.map((p) => parseScalar(p)).filter((p) => p !== null || parts.length === 0);
  }
  if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) {
    return s.slice(1, -1).replace(/\\"/g, '"');
  }
  if (s === 'true') return true;
  if (s === 'false') return false;
  if (s === 'null' || s === '~' || s === '') return null;
  const n = Number(s);
  return s !== '' && Number.isFinite(n) ? n : s;
}

function parseBlock(lines, start, indent) {
  // Returns [value, nextIndex]. Detects list vs map by first meaningful line.
  let i = start;
  const NE = () => {
    while (i < lines.length && (lines[i].trim() === '' || lines[i].trimStart().startsWith('#'))) i++;
    return i;
  };
  i = NE();
  if (i >= lines.length) return [null, i];
  const first = lines[i];
  const firstIndent = first.length - first.trimStart().length;
  if (firstIndent < indent) return [undefined, start];
  const isList = first.trimStart().startsWith('- ');
  if (!isList) {
    const map = {};
    while (true) {
      i = NE();
      if (i >= lines.length) break;
      const line = lines[i];
      const ind = line.length - line.trimStart().length;
      if (ind < indent || line.trimStart().startsWith('- ')) break;
      const m = line.trim().match(/^([^:]+):\s*(.*)$/);
      if (!m) { i++; continue; }
      const key = m[1].trim();
      if (m[2] !== '') {
        map[key] = parseScalar(m[2]);
        i++;
      } else {
        const [val, next] = parseBlock(lines, i + 1, ind + 2);
        map[key] = val === undefined ? null : val;
        i = next;
      }
    }
    return [map, i];
  }
  const list = [];
  while (true) {
    i = NE();
    if (i >= lines.length) break;
    const line = lines[i];
    const ind = line.length - line.trimStart().length;
    if (ind < indent || !line.trimStart().startsWith('- ')) break;
    const rest = line.trimStart().slice(2);
    const m = rest.match(/^([^:]+):\s*(.*)$/);
    if (m && (m[2] !== '' || true)) {
      // List item that opens a map: "- key: value" possibly with nested lines.
      const item = {};
      if (m[2] !== '') item[m[1].trim()] = parseScalar(m[2]);
      const [nested, next] = parseBlock(lines, i + 1, ind + 2);
      if (nested && typeof nested === 'object' && !Array.isArray(nested)) Object.assign(item, nested);
      if (m[2] === '' && (!nested || typeof nested !== 'object')) { i++; continue; }
      list.push(item);
      i = next;
    } else {
      list.push(parseScalar(rest));
      i++;
    }
  }
  return [list, i];
}

function parseYaml(text) {
  const [val] = parseBlock(text.split('\n'), 0, 0);
  return val ?? {};
}

function loadContext(name) {
  const dir = join(contextsDir, name);
  const ledgerFile = join(dir, 'evidence-ledger.md');
  if (!existsSync(ledgerFile)) return null;
  const raw = readFileSync(ledgerFile, 'utf8');
  const fence = raw.split('```yaml')[1]?.split('```')[0] ?? '';
  let ledger = {};
  try {
    ledger = parseYaml(fence);
  } catch (err) {
    console.error(`WARN: could not parse ledger for ${name}: ${err.message}`);
  }
  let rulesStatus = '';
  const rulesFile = join(dir, 'business-rules.md');
  if (existsSync(rulesFile)) {
    const first = readFileSync(rulesFile, 'utf8').split('\n').slice(0, 6).join('\n');
    const m = first.match(/Status:\s*([^\n.]+)/);
    if (m) rulesStatus = m[1].trim();
  }
  return { name, ledger, rulesStatus };
}

const esc = (v) =>
  String(v ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

const gateBadge = (state) =>
  state === 'passed'
    ? '<span class="pill pass">passed</span>'
    : state === 'partial'
      ? '<span class="pill partial">partial</span>'
      : '<span class="pill skip">skipped</span>';

const contexts = readdirSync(contextsDir, { withFileTypes: true })
  .filter((d) => d.isDirectory())
  .map((d) => loadContext(d.name))
  .filter(Boolean)
  .sort((a, b) => a.name.localeCompare(b.name));

const gates = ['G0', 'G1', 'G2', 'G3', 'G4'];
const counts = { passed: 0, partial: 0, skipped: 0 };
for (const c of contexts) {
  for (const g of gates) {
    const s = c.ledger.gates?.[g];
    if (s === 'passed') counts.passed++;
    else if (s === 'partial') counts.partial++;
    else counts.skipped++;
  }
}

function kvList(rows) {
  if (!rows || !rows.length) return '<p class="muted">—</p>';
  return `<ul class="tight">${rows.map((r) => `<li>${esc(typeof r === 'string' ? r : JSON.stringify(r))}</li>`).join('')}</ul>`;
}

function commandsTable(commands) {
  if (!commands || !commands.length) return '<p class="muted">No recorded evidence yet.</p>';
  return `<table><thead><tr><th>Command</th><th>Result</th></tr></thead><tbody>${commands
    .map((c) => `<tr><td><code>${esc(c.command)}</code></td><td>${esc(c.result)}</td></tr>`)
    .join('')}</tbody></table>`;
}

function risksTable(risks) {
  if (!risks || !risks.length) return '<p class="muted">No open risks. 🎉</p>';
  return `<table><thead><tr><th>Risk</th><th>Owner</th><th>Review by</th></tr></thead><tbody>${risks
    .map(
      (r) =>
        `<tr><td>${esc(r.risk)}${r.disposition ? `<br><span class="muted">${esc(r.disposition)}</span>` : ''}</td><td>${esc(r.owner)}</td><td>${esc(r.review_by)}</td></tr>`,
    )
    .join('')}</tbody></table>`;
}

function failuresList(failures) {
  if (!failures || !failures.length) return '<p class="muted">None recorded.</p>';
  return `<div>${failures
    .map(
      (f) =>
        `<details><summary><span class="tag">${esc(f.class)}</span> ${esc((f.evidence_for?.[0] ?? '')).slice(0, 110)}…</summary><p><strong>For:</strong> ${esc((f.evidence_for ?? []).join('; '))}</p><p><strong>Against:</strong> ${esc((f.evidence_against ?? []).join('; '))}</p><p><strong>Verdict:</strong> ${esc(f.verdict)}</p></details>`,
    )
    .join('')}</div>`;
}

const sections = contexts
  .map((c) => {
    const L = c.ledger;
    const roles = (L.roles_covered ?? []).map((r) => `<li><strong>${esc(r.role)}</strong> → <code>${esc(r.happy_path)}</code></li>`).join('');
    const skips = (L.skip_reasons ?? []).map((s) => `<li><strong>${esc(s.gate)}</strong>: ${esc(s.reason)}</li>`).join('');
    return `<section id="${esc(c.name)}">
<h2>${esc(c.name)} <span class="muted">· ${esc(L.status ?? '')}${c.rulesStatus ? ` · rules ${esc(c.rulesStatus)}` : ''}</span></h2>
<p class="journey">${esc(L.journey ?? '')}</p>
<div class="gates">${gates.map((g) => `<span><strong>${g}</strong> ${gateBadge(L.gates?.[g])}</span>`).join('')}</div>
${skips ? `<h3>Skipped gates</h3><ul class="tight">${skips}</ul>` : ''}
<h3>Evidence</h3>${commandsTable(L.commands)}
<h3>Open risks</h3>${risksTable(L.open_risks)}
<h3>Actor coverage</h3>${roles ? `<ul class="tight">${roles}</ul>` : '<p class="muted">—</p>'}
<h3>Findings</h3>${failuresList(L.failures)}
</section>`;
  })
  .join('\n');

const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>IoBuild · Convergent Testing evidence</title>
<style>
:root { color-scheme: light; }
body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; max-width: 1080px; margin: 0 auto; padding: 2rem 1.25rem 4rem; color: #1e293b; background: #f8fafc; }
h1 { font-size: 1.9rem; margin-bottom: 0.25rem; }
h2 { font-size: 1.35rem; margin-top: 0; border-bottom: 2px solid #e2e8f0; padding-bottom: 0.35rem; }
h3 { font-size: 1.05rem; margin: 1.25rem 0 0.5rem; }
section { background: #fff; border: 1px solid #e2e8f0; border-radius: 0.75rem; padding: 1.25rem 1.5rem; margin: 1.25rem 0; }
table { width: 100%; border-collapse: collapse; font-size: 0.86rem; }
th, td { text-align: left; padding: 0.5rem 0.6rem; border-bottom: 1px solid #eef2f7; vertical-align: top; }
th { color: #64748b; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em; }
code { background: #f1f5f9; padding: 0.1rem 0.35rem; border-radius: 0.3rem; font-size: 0.82em; overflow-wrap: anywhere; }
.pill { display: inline-block; padding: 0.1rem 0.55rem; border-radius: 9999px; font-size: 0.75rem; font-weight: 700; }
.pass { background: #ecfdf5; color: #065f46; border: 1px solid #a7f3d0; }
.partial { background: #fffbeb; color: #92400e; border: 1px solid #fde68a; }
.skip { background: #f1f5f9; color: #64748b; border: 1px solid #e2e8f0; }
.tag { background: #eef2ff; color: #4338ca; border-radius: 0.3rem; padding: 0.05rem 0.4rem; font-size: 0.75rem; font-weight: 700; }
.muted { color: #64748b; }
.journey { font-style: italic; color: #475569; }
.gates { display: flex; gap: 1rem; flex-wrap: wrap; margin: 0.5rem 0; }
.tight { margin: 0.25rem 0; padding-left: 1.25rem; }
.tight li { margin: 0.2rem 0; }
details { border: 1px solid #eef2f7; border-radius: 0.5rem; padding: 0.5rem 0.75rem; margin: 0.4rem 0; font-size: 0.88rem; }
summary { cursor: pointer; }
.matrix td, .matrix th { text-align: center; }
.matrix td:first-child, .matrix th:first-child { text-align: left; }
nav.toc a { margin-right: 0.9rem; }
header p { color: #475569; }
</style>
</head>
<body>
<header>
<h1>Convergent Testing · evidence dashboard</h1>
<p>Generated from <code>docs/bounded-contexts/*/evidence-ledger.md</code> — the ledgers are the source of truth; this page is a view. ${new Date().toISOString().slice(0, 10)}</p>
<p><strong>${counts.passed}</strong> passed · <strong>${counts.partial}</strong> partial · <strong>${counts.skipped}</strong> skipped gates across ${contexts.length} contexts.</p>
<nav class="toc">${contexts.map((c) => `<a href="#${esc(c.name)}">${esc(c.name)}</a>`).join('')}</nav>
</header>
<h2>Gate matrix</h2>
<table class="matrix"><thead><tr><th>Context</th>${gates.map((g) => `<th>${g}</th>`).join('')}</tr></thead>
<tbody>${contexts.map((c) => `<tr><td><a href="#${esc(c.name)}">${esc(c.name)}</a></td>${gates.map((g) => `<td>${gateBadge(c.ledger.gates?.[g])}</td>`).join('')}</tr>`).join('')}</tbody></table>
${sections}
<footer><p class="muted">Regenerate any time: <code>node scripts/evidence-dashboard.mjs</code></p></footer>
</body>
</html>`;

writeFileSync(outPath, html);
console.log(`Dashboard written to ${outPath} (${contexts.length} contexts).`);
