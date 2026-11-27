import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { createI18n } from 'vue-i18n'
import en from '../../src/locales/en.json'
import es from '../../src/locales/es.json'

const source = readFileSync(
  new URL('../../src/devices/presentation/components/AddCustomDeviceDialog.vue', import.meta.url),
  'utf8'
)
const usedKeys = [...new Set(source.match(/devices\.addDialog\.[A-Za-z.]*[A-Za-z]/g))]

describe('Add Device dialog translations', () => {
  it('references translation keys', () => {
    expect(usedKeys.length).toBeGreaterThanOrEqual(20)
  })

  it.each(['en', 'es'])('resolves every dialog key in %s without falling back', (locale) => {
    const i18n = createI18n({ legacy: false, locale, messages: { en, es } })

    expect(usedKeys.filter((key) => !i18n.global.te(key, locale))).toEqual([])
  })

  it('keeps no literal visible text in the template', () => {
    const template = source.slice(source.indexOf('<template>'), source.indexOf('<style'))

    expect(template).not.toMatch(/\s(label|header|placeholder)="/)
    expect(template).not.toMatch(/Add Device|Device Type|Device Name|Assign to Unit|Select a unit/)
  })
})
