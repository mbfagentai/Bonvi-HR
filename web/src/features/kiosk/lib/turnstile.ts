import type { KioskLanguage } from '../api/kiosk-api'

/**
 * Cloudflare Turnstile — the invisible bot check on the public form
 * (SPEC-planshet-anketa §2.3 item 2). No npm package: Turnstile is one global
 * script, and the form needs only `render`, `reset` and `remove` of it.
 *
 * The script is added to the page the first time the form's last screen asks
 * for it, and only when the session carries a site key — never on the HR
 * pages, and not at all while the check is off (dev, tests, until the owner
 * adds the keys). It must be loaded from this exact address: Cloudflare does
 * not support proxying or caching `api.js`.
 */

const SCRIPT_SRC = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit'

/** A script that neither loads nor fails within this long counts as unavailable. */
const LOAD_TIMEOUT_MS = 15_000

/** Render parameters this page uses (developers.cloudflare.com/turnstile, «Widget configurations»). */
export interface TurnstileRenderOptions {
  sitekey: string
  /** `auto` or a supported ISO 639-1 code. */
  language: string
  theme: 'auto' | 'light' | 'dark'
  size: 'normal' | 'flexible' | 'compact'
  appearance: 'always' | 'execute' | 'interaction-only'
  retry: 'auto' | 'never'
  'refresh-expired': 'auto' | 'manual' | 'never'
  /** `false` — no hidden `cf-turnstile-response` input; the token is sent by hand. */
  'response-field': boolean
  'feedback-enabled': boolean
  callback: (token: string) => void
  /** A truthy return tells Turnstile the error was handled (no console warning). */
  'error-callback': (errorCode: string) => boolean
  'expired-callback': () => void
  'timeout-callback': () => void
  'before-interactive-callback': () => void
}

export interface TurnstileApi {
  /** The widget id, or nothing when the widget could not be rendered. */
  render(container: HTMLElement, options: TurnstileRenderOptions): string | null | undefined
  reset(widgetId: string): void
  remove(widgetId: string): void
}

declare global {
  interface Window {
    turnstile?: TurnstileApi
  }
}

let pending: Promise<TurnstileApi> | null = null

/**
 * Loads `api.js` once and resolves with `window.turnstile`. A network error
 * removes the tag so the next attempt adds it again; a timeout keeps it (it
 * may still arrive) — either way the next call tries again instead of
 * returning the old failure.
 */
export function loadTurnstile(): Promise<TurnstileApi> {
  const ready = window.turnstile
  if (ready !== undefined) return Promise.resolve(ready)
  if (pending !== null) return pending

  const attempt = new Promise<TurnstileApi>((resolve, reject) => {
    let script = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT_SRC}"]`)
    if (script === null) {
      script = document.createElement('script')
      script.src = SCRIPT_SRC
      script.async = true
      document.head.append(script)
    }
    const element = script

    const finish = (outcome: 'load' | 'error' | 'timeout') => {
      window.clearTimeout(timer)
      element.removeEventListener('load', onLoad)
      element.removeEventListener('error', onError)
      pending = null

      const api = window.turnstile
      if (outcome !== 'error' && api !== undefined) {
        resolve(api)
        return
      }
      if (outcome === 'error') element.remove()
      reject(new Error(`turnstile: ${outcome}`))
    }
    const onLoad = () => finish('load')
    const onError = () => finish('error')
    const timer = window.setTimeout(() => finish('timeout'), LOAD_TIMEOUT_MS)

    element.addEventListener('load', onLoad)
    element.addEventListener('error', onError)
  })

  pending = attempt
  return attempt
}

/**
 * Turnstile speaks Russian but not Uzbek (developers.cloudflare.com/turnstile/
 * reference/supported-languages) — for Uzbek it follows the browser's own
 * language, which beats the English it would fall back to for `uz`.
 */
export function turnstileLanguage(language: KioskLanguage): 'ru' | 'auto' {
  return language === 'ru' ? 'ru' : 'auto'
}
