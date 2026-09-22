import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

import en from './locales/en.json'
import ru from './locales/ru.json'
import uzCyrl from './locales/uz-Cyrl.json'
import uzLatn from './locales/uz-Latn.json'
import { DEFAULT_LOCALE, LOCALE_CODES } from './locales'

/**
 * Til sozlamasining kaliti. HR ilovasi `bonvi.lang` ishlatadi — bu yerda
 * `bonvi.ilova.lang`: ikkala ilova bitta domenda yashaydi va bitta
 * `localStorage` ni baham ko'radi, ya'ni umumiy kalit HR xodimining
 * tilini xodim ilovasidan o'zgartirib yuborardi.
 */
export const LANG_STORAGE_KEY = 'bonvi.ilova.lang'

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
      ru: { translation: ru },
      'uz-Latn': { translation: uzLatn },
      'uz-Cyrl': { translation: uzCyrl },
    },
    supportedLngs: LOCALE_CODES as unknown as string[],
    fallbackLng: DEFAULT_LOCALE,

    // «uz-Latn» ni «uz» ga qisqartirmaslik kerak — yozuv ma'lumoti yo'qoladi
    load: 'currentOnly',
    nonExplicitSupportedLngs: false,

    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: LANG_STORAGE_KEY,
      caches: ['localStorage'],
    },

    interpolation: {
      // React o'zi ekranlaydi
      escapeValue: false,
    },

    returnNull: false,
  })

export default i18n
