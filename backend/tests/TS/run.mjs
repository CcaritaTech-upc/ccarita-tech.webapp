import { spawnSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const match = /^(?:TS)?0?([1-5])$/i.exec(process.argv[2] ?? '')

if (!match) {
  console.error('Usage: npm run test:ts -- <01-05>')
  process.exit(2)
}

const story = `TS${match[1].padStart(2, '0')}`
const scriptDirectory = dirname(fileURLToPath(import.meta.url))
const repositoryRoot = resolve(scriptDirectory, '../../..')
const testProject = resolve(repositoryRoot, 'backend/tests/Modules/IoBuild.Modules.Tests.csproj')

const result = spawnSync(
  'dotnet',
  ['test', testProject, '--filter', `TechnicalStory=${story}`],
  { cwd: repositoryRoot, stdio: 'inherit' }
)

if (result.error) {
  console.error(`Could not start dotnet test: ${result.error.message}`)
  process.exit(1)
}

process.exit(result.status ?? 1)
