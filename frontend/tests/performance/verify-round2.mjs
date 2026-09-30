import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
const repo = resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const root = resolve(process.argv[2]);
let links = 0, sources = 0;
for (const file of ['docs/performance/evidence-ledger.md', ...['iam', 'profiles', 'analytics', 'devices', 'publishing', 'subscriptions'].map(c => `docs/bounded-contexts/${c}/evidence-ledger.md`)]) {
  const text = readFileSync(join(repo, file), 'utf8');
  for (const match of text.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)) {
    if (/^https?:/.test(match[1])) continue;
    const [path, anchor] = match[1].split('#');
    const target = path ? resolve(dirname(join(repo, file)), path) : join(repo, file);
    if (!existsSync(target)) throw new Error('Missing link ' + match[1]);
    if (anchor) {
      const ids = [...readFileSync(target, 'utf8').matchAll(/^#+\s+(.+)$/gm)].map(m => m[1].toLowerCase().replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/ /g, '-'));
      if (!ids.includes(anchor)) throw new Error('Missing anchor ' + anchor);
    }
    links++;
  }
  if (file.includes('bounded-contexts')) {
    const historical = execFileSync('git', ['show', 'HEAD:' + file], { cwd: repo, encoding: 'utf8' });
    const yaml = s => s.slice(s.indexOf('```yaml')).replace(/\r\n/g, '\n');
    if (yaml(text) !== yaml(historical)) throw new Error('Historical YAML changed: ' + file);
  }
}
function compare(path) {
  for (const entry of readdirSync(join(root, path), { withFileTypes: true })) {
    const p = join(path, entry.name);
    if (entry.isDirectory()) { if (!['bin', 'obj', 'node_modules', 'dist', 'tests'].includes(entry.name)) compare(p); }
    else if (/\.(cs|csproj|sln|props|targets|vue|js|html)$/.test(entry.name) || ['Dockerfile', 'nginx.conf', 'package.json', 'package-lock.json'].includes(entry.name)) {
      const hash = file => createHash('sha256').update(readFileSync(file)).digest('hex');
      if (hash(join(root, p)) !== hash(join(repo, p))) throw new Error('Snapshot differs from checkout: ' + p);
      sources++;
    }
  }
}
compare('frontend'); compare('backend');
console.log(JSON.stringify({ localLinks: links, historicalYaml: 'unchanged', matchingSnapshotApplicationFiles: sources }, null, 2));
