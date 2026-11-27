import { describe, expect, it } from 'vitest'
import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createI18n } from 'vue-i18n'
import en from '../../src/locales/en.json'
import es from '../../src/locales/es.json'

const src = fileURLToPath(new URL('../../src', import.meta.url))
const read = (path) => readFileSync(join(src, path), 'utf8')

const components = [
  'devices/presentation/components/my-unit-devices.vue',
  'devices/presentation/components/device-control-panel.vue',
  'analytics/presentation/components/owner-dashboard.component.vue',
  'analytics/presentation/components/unit-card.component.vue',
  'projects/presentation/views/project-grid.vue'
]

describe('Owner flow and project list translations', () => {
  const usedKeys = [...new Set(components.flatMap((path) =>
    [...read(path).matchAll(/(?<![\w.])\$?t\(\s*(?:[\w.]+(?:\([^)]*\))?\s*\?\s*)?['"]([\w.-]+)['"]/g)].map((m) => m[1])))]

  it.each(['en', 'es'])('resolves every referenced key in %s without falling back', (locale) => {
    const i18n = createI18n({ legacy: false, locale, messages: { en, es } })

    expect(usedKeys.length).toBeGreaterThan(30)
    expect(usedKeys.filter((key) => !i18n.global.te(key, locale))).toEqual([])
  })

  it('has no untranslated literals left in those screens', () => {
    const literals = /My Unit Devices|header="(Name|Status|Last Online|Control)"|label="Send"|Live Energy —|Auto-refreshes|Gestiona tus proyectos|Aún no has registrado/

    for (const path of components) {
      expect(read(path), path).not.toMatch(literals)
    }
  })
})

describe('Analytics store calls from components', () => {
  const store = read('analytics/application/analytics.store.js')
  const exported = store.slice(store.lastIndexOf('return {'))

  it('only calls actions the store actually exposes', () => {
    const files = readdirSync(src, { recursive: true }).filter((file) => file.endsWith('.vue'))
    const unknown = files.flatMap((file) =>
      [...read(file).matchAll(/analyticsStore\.(\w+)\(/g)]
        .map((m) => m[1])
        .filter((name) => !new RegExp(`\\b${name}\\b`).test(exported))
        .map((name) => `${file}: ${name}`))

    expect(unknown).toEqual([])
  })
})
