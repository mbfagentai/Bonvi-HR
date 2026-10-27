/// <reference types="vite/client" />

/**
 * Loyihaning o'z muhit o'zgaruvchilari.
 *
 * `VITE_DEV_*` — faqat ishlab chiqish uchun (kirish sahifasidagi tez kirish
 * paneli). Production build'da `vite.config.ts` ularni `undefined` qiladi.
 *
 * `VITE_ANKETA_URL` — optional short public address of the candidate form
 * (`https://anketa.bonvi.uz`, SPEC-planshet-anketa §7); HR's «Anketa havolasi»
 * shows it instead of `<origin>/anketa` when set.
 */
interface ImportMetaEnv {
  readonly VITE_API_URL?: string
  readonly VITE_ANKETA_URL?: string
  readonly VITE_DEV_LOGIN?: string
  readonly VITE_DEV_PASSWORD?: string
  readonly VITE_DEV_LABEL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
