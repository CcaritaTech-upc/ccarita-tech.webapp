import { describe, expect, it } from 'vitest'
import { createI18n } from 'vue-i18n'
import en from '../../src/locales/en.json'
import es from '../../src/locales/es.json'

describe('Builder project success toast translations', () => {
  it.each([
    ['en', 'Success'],
    ['es', 'Éxito']
  ])('resolves common.success in %s', (locale, expected) => {
    const i18n = createI18n({
      legacy: false,
      locale,
      messages: { en, es }
    })

    expect(i18n.global.t('common.success')).toBe(expected)
  })
})
