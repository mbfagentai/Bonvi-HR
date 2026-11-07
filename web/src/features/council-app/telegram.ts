/** The part of Telegram's Mini App script this page uses. */
interface TelegramWebApp {
  initData: string
  initDataUnsafe: { start_param?: string }
  ready(): void
  expand(): void
  /** Since 7.0; older clients lack it. */
  isVersionAtLeast?(version: string): boolean
  /** Bot API 8.0: the whole screen, Telegram's own controls float over the top (`--tg-content-safe-area-inset-top`). */
  requestFullscreen?(): void
  /** Bot API 7.7: scrolling a long PDF must not swipe the app closed. */
  disableVerticalSwipes?(): void
}

/** Whole screen where the client can: a questionnaire page is unreadable in the half-height sheet or a small desktop window. */
function enlarge(app: TelegramWebApp) {
  app.expand()

  try {
    if (app.isVersionAtLeast?.('7.7')) app.disableVerticalSwipes?.()
    if (app.isVersionAtLeast?.('8.0')) app.requestFullscreen?.()
  } catch {
    // An old client throws «WebAppMethodUnsupported»: the expanded sheet stays, which is what it showed before.
  }
}

declare global {
  interface Window {
    Telegram?: { WebApp?: TelegramWebApp }
  }
}

const SCRIPT_URL = 'https://telegram.org/js/telegram-web-app.js'

/** Telegram's script, loaded only for this page so the rest of the site keeps its strict script rule. */
export function loadTelegram(): Promise<TelegramWebApp | null> {
  return new Promise((resolve) => {
    const done = () => {
      const app = window.Telegram?.WebApp ?? null
      app?.ready()
      if (app !== null) enlarge(app)
      resolve(app)
    }

    if (window.Telegram?.WebApp) {
      done()
      return
    }

    const script = document.createElement('script')
    script.src = SCRIPT_URL
    script.onload = done
    script.onerror = () => resolve(null)
    document.head.append(script)
  })
}

const COMPACT_UUID = /^[0-9a-f]{32}$/iu

/** The candidate a button pointed at: `startapp` from the group, `nomzod` from the private chat's button. */
export function targetCandidate(app: TelegramWebApp): string | null {
  const raw = app.initDataUnsafe.start_param ?? new URLSearchParams(window.location.search).get('nomzod')

  return raw !== null && raw !== undefined && COMPACT_UUID.test(raw) ? raw : null
}
