import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

import en from './locales/en.json'
import ru from './locales/ru.json'
import uzCyrl from './locales/uz-Cyrl.json'
import uzLatn from './locales/uz-Latn.json'
import { DEFAULT_LOCALE, LOCALE_CODES } from './locales'

export const STORAGE_KEY = 'bonvi.lang'

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
    supportedLngs: LOCALE_CODES,
    fallbackLng: DEFAULT_LOCALE,

    // «uz-Latn» ni «uz» ga qisqartirmaslik kerak — yozuv ma'lumoti yo'qoladi
    load: 'currentOnly',
    nonExplicitSupportedLngs: false,

    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: STORAGE_KEY,
      caches: ['localStorage'],
    },

    interpolation: {
      // React o'zi ekranlaydi
      escapeValue: false,
    },

    returnNull: false,
  })

export default i18n
