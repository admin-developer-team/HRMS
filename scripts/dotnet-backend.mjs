import { realpathSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const repositoryRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const backendRoot = realpathSync(join(repositoryRoot, 'backend'));
const action = process.argv[2] ?? 'run';

const commands = {
  run: ['run', '--project', join(backendRoot, 'src', 'Hrms.Api', 'Hrms.Api.csproj'), '--launch-profile', 'http'],
  build: ['build', join(backendRoot, 'Hrms.slnx')],
  test: ['test', join(backendRoot, 'Hrms.slnx')],
};

const args = commands[action];

if (!args) {
  console.error(`Unknown backend action: ${action}`);
  process.exit(2);
}

const result = spawnSync('dotnet', args, {
  cwd: backendRoot,
  stdio: 'inherit',
  shell: process.platform === 'win32',
});

if (result.error) {
  console.error(result.error.message);
  process.exit(1);
}

process.exit(result.status ?? 1);
