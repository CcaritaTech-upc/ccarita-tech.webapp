import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const frontendDirectory = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const repositoryRoot = path.resolve(frontendDirectory, '..');
const rawStory = process.argv[2] ?? '';
const storyNumber = rawStory.toUpperCase().replace(/^US/, '');

if (!/^\d{1,2}$/.test(storyNumber) || Number(storyNumber) < 1 || Number(storyNumber) > 10) {
  console.error('Usage: npm run test:us -- <01-10 | US01-US10>');
  process.exit(2);
}

const storyTag = `US${String(Number(storyNumber)).padStart(2, '0')}`;
const featureDirectory = path.join(repositoryRoot, 'test', 'Features');
const featureFile = readdirSync(featureDirectory).find((name) => name.startsWith(storyTag) && name.endsWith('.feature'));

if (!featureFile) {
  console.error(`No Gherkin feature was found for ${storyTag} in test/Features/.`);
  process.exit(2);
}

if (!readFileSync(path.join(featureDirectory, featureFile), 'utf8').includes(`@${storyTag}`)) {
  console.error(`${featureFile} must declare the @${storyTag} tag before it can be run by story ID.`);
  process.exit(2);
}

if (!existsSync(path.join(frontendDirectory, 'node_modules', '@cucumber', 'cucumber', 'package.json'))) {
  console.error('Cucumber is not installed. Run `npm ci` from frontend/ first.');
  process.exit(2);
}

function assertLocalDockerContext() {
  const contextName = execFileSync('docker', ['context', 'show'], {
    cwd: repositoryRoot,
    encoding: 'utf8',
  }).trim();
  const context = JSON.parse(execFileSync('docker', ['context', 'inspect', contextName], {
    cwd: repositoryRoot,
    encoding: 'utf8',
  }))[0];
  const host = process.env.DOCKER_HOST || context?.Endpoints?.docker?.Host || '';
  const localEngine = /^(npipe:|unix:\/\/|tcp:\/\/(localhost|127\.0\.0\.1)(:|\/))/i.test(host);

  if (!localEngine) {
    throw new Error(`Refusing to run test data setup against non-local Docker context "${contextName}" (${host || 'unknown endpoint'}).`);
  }
}

function run(command, args, options) {
  const result = spawnSync(command, args, {
    ...options,
    stdio: 'inherit',
    shell: process.platform === 'win32',
  });
  if (result.error) throw result.error;
  return result.status ?? 1;
}

const projectName = `iobuild-bdd-${storyTag.toLowerCase()}-${process.pid}`;
const composeArgs = [
  'compose',
  '-p', projectName,
  '-f', 'docker-compose.yml',
  '-f', 'docker-compose.bdd.yml',
];
const testEnvironment = {
  ...process.env,
  COMPOSE_PROFILES: '',
  DB_PASSWORD: 'bdd-test-only-password',
  ConnectionStrings__IoBuild: 'Server=mysql-monolith;Port=3306;Database=iobuild;User=root;Password=bdd-test-only-password',
  JWT_SECRET: 'bdd-test-only-jwt-secret-value-32-characters-minimum',
  STRIPE_USE_SIMULATED_PAYMENTS: 'true',
  STRIPE_SECRET_KEY: '',
  STRIPE_RESTRICTED_API_KEY: '',
  STRIPE_WEBHOOK_SECRET: 'whsec_bdd_test_only',
  VITE_STRIPE_PUBLISHABLE_KEY: 'pk_test_bdd_runner',
  CLOUDINARY_CLOUD_NAME: '',
  VITE_CLOUDINARY_UPLOAD_PRESET: '',
  E2E_BASE_URL: 'http://127.0.0.1:18081',
  E2E_NGINX: '1',
  E2E_CLOUDINARY_DUMMY: '1',
  E2E_SIMULATED_PAYMENTS: '1',
};

let exitCode = 1;
let composeAttempted = false;

try {
  assertLocalDockerContext();
  console.log(`Running ${storyTag} against a disposable local Compose stack...`);
  composeAttempted = true;

  const upResult = run('docker', [...composeArgs, 'up', '-d', '--wait'], {
    cwd: repositoryRoot,
    env: testEnvironment,
  });
  if (upResult !== 0) throw new Error(`docker compose up failed with exit code ${upResult}`);

  const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  exitCode = run(npmCommand, ['run', 'test:bdd', '--', '--tags', `@${storyTag}`], {
    cwd: frontendDirectory,
    env: testEnvironment,
  });
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  exitCode = 1;
} finally {
  if (composeAttempted) {
    const cleanupResult = run('docker', [...composeArgs, 'down', '-v', '--remove-orphans'], {
      cwd: repositoryRoot,
      env: testEnvironment,
    });
    if (exitCode === 0 && cleanupResult !== 0) exitCode = cleanupResult;
  }
}

process.exitCode = exitCode;
