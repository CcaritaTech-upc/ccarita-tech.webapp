import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

// Validate with the same parser/image as the production frontend container.
const config = fileURLToPath(new URL('../nginx.conf', import.meta.url));
const result = spawnSync('docker', [
  'run', '--rm', '--network=none',
  '--mount', `type=bind,source=${config},target=/etc/nginx/conf.d/default.conf,readonly`,
  'nginx:alpine', 'nginx', '-t',
], { stdio: 'inherit' });
if (result.error) throw result.error;
process.exit(result.status ?? 1);
