import { cpSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = resolve(fileURLToPath(new URL('..', import.meta.url)));
const root = resolve(process.argv[2]);
const temp = resolve(process.env.TEMP || process.env.TMP || 'C:/Users/halli/AppData/Local/Temp', 'opencode');
if (!root.startsWith(temp + '\\') && !root.startsWith(temp + '/')) throw new Error('Snapshot must be below authorized Temp/opencode');
mkdirSync(root, { recursive: true });
const front = join(root, 'frontend');
mkdirSync(front, { recursive: true });
for (const path of ['src', 'public', 'tests', 'package.json', 'package-lock.json', 'index.html', 'vite.config.js', 'vitest.config.js', 'playwright.config.js', 'Dockerfile', 'nginx.conf', '.dockerignore']) {
  cpSync(join(repo, 'frontend', path), join(front, path), { recursive: true, filter: p => !/(^|[\\/])\.env/.test(p) });
}
// These public fixture settings are synthesized; checkout dotenv is never read.
writeFileSync(join(front, '.env.production'), ['API_URL=/api/v1', 'USERS_ENDPOINT_PATH=/users', 'IAM_SESSIONS_PATH=/sessions', 'PROFILES_ENDPOINT_PATH=/profiles', 'CLIENTS_ENDPOINT_PATH=/clients', 'PROJECTS_ENDPOINT_PATH=/projects', 'UNITS_ENDPOINT_PATH=/units', 'DEVICES_ENDPOINT_PATH=/devices', 'ANALYTICS_ENDPOINT_PATH=/analytics', 'PLANS_ENDPOINT_PATH=/plans', 'SUBSCRIPTIONS_ENDPOINT_PATH=/subscriptions'].map(s => 'VITE_' + s).join('\n'));
const back = join(root, 'backend');
mkdirSync(back, { recursive: true });
function copyCode(source, target) {
  for (const entry of readdirSync(source, { withFileTypes: true })) {
    if (['bin', 'obj', 'node_modules', '.git'].includes(entry.name)) continue;
    const from = join(source, entry.name), to = join(target, entry.name);
    if (entry.isDirectory()) { mkdirSync(to, { recursive: true }); copyCode(from, to); }
    else if (/\.(cs|csproj|sln|props|targets)$/.test(entry.name) || ['Dockerfile', '.dockerignore', 'NuGet.Config', 'nuget.config'].includes(entry.name)) cpSync(from, to);
  }
}
copyCode(join(repo, 'backend'), back);
writeFileSync(join(back, 'src/IoBuild.Api/appsettings.json'), '{}\n');
cpSync(join(repo, 'nginx/nginx.conf'), join(root, 'outer-nginx.conf'));
writeFileSync(join(root, 'empty.env'), '');
writeFileSync(join(root, 'compose.yml'), `services:
  mysql-monolith:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: round2-local-fixture
      MYSQL_DATABASE: iobuild
    volumes: [audit-db:/var/lib/mysql]
    healthcheck:
      test: [CMD, mysqladmin, ping, -h, localhost]
      interval: 3s
      timeout: 3s
      retries: 40
  iobuild-api:
    build: ./backend
    environment:
      ASPNETCORE_URLS: http://+:8080
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__IoBuild: Server=mysql-monolith;Port=3306;Database=iobuild;User=root;Password=round2-local-fixture;
      Jwt__Secret: round2-local-lighthouse-fixture-only-32chars
      Migrations__ApplyOnStartup: 'true'
      Cors__AllowedOrigins: '*'
      Mqtt__Enabled: 'false'
      Stripe__UseSimulatedPayments: 'true'
      Stripe__RestrictedApiKey: rk_test_local
    depends_on:
      mysql-monolith: {condition: service_healthy}
  frontend:
    build:
      context: ./frontend
      args:
        VITE_CLOUDINARY_CLOUD_NAME: local-performance-dummy
        VITE_CLOUDINARY_UPLOAD_PRESET: local-performance-dummy
        VITE_STRIPE_PUBLISHABLE_KEY: pk_test_local_performance_dummy
  nginx:
    image: nginx:alpine
    ports: ['18081:80']
    volumes: ['./outer-nginx.conf:/etc/nginx/conf.d/default.conf:ro']
    depends_on: [iobuild-api, frontend]
volumes:
  audit-db:
`);
console.log('Safe snapshot prepared:', root);
