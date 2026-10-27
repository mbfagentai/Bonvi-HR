import { useEffect, useLayoutEffect, useRef, useState } from 'react'

import type { KioskLanguage } from '../api/kiosk-api'
import { loadTurnstile, turnstileLanguage, type TurnstileApi } from '../lib/turnstile'

export type BotCheckState =
  /** `interactive` — Turnstile shows a checkbox here and waits for a tap. */
  | { status: 'checking'; interactive: boolean }
  | { status: 'passed'; token: string }
  | { status: 'failed' }

const CHECKING: BotCheckState = { status: 'checking', interactive: false }

/** Automatic retries after a failed check; then it waits for the candidate's next «Yuborish». */
const AUTO_RETRIES = 2
const RETRY_DELAY_MS = 4000

/**
 * The Turnstile widget on the form's last screen (SPEC §2.3 item 2). Managed
 * mode with `interaction-only`: most people never see it; a suspicious
 * browser gets a checkbox here. The token stays in memory (`onChange`) and
 * is never written anywhere.
 *
 * A fresh challenge runs when the token expires, after a failed check (twice
 * on its own, then on the parent's next `resetSignal`), and whenever the
 * parent bumps `resetSignal` — after any failed submit, since a token is good
 * for one server check only. Unmounting removes the widget, so the next
 * candidate on the office tablet always starts with a new one.
 */
export function BotCheck({
  siteKey,
  language,
  resetSignal,
  onChange,
}: {
  siteKey: string
  language: KioskLanguage
  /** Bump to throw the current token away and run the check again. */
  resetSignal: number
  onChange: (state: BotCheckState) => void
}) {
  const containerRef = useRef<HTMLDivElement>(null)
  const widgetRef = useRef<{ api: TurnstileApi; id: string } | null>(null)
  const retriesRef = useRef(0)
  const retryTimerRef = useRef<number | undefined>(undefined)
  /** Bumped to load and render again when the script itself could not be loaded. */
  const [attempt, setAttempt] = useState(0)

  // The widget keeps the callbacks it was rendered with; they reach the parent's latest handler.
  const onChangeRef = useRef(onChange)
  useLayoutEffect(() => {
    onChangeRef.current = onChange
  })

  const widgetLanguage = turnstileLanguage(language)

  useEffect(() => {
    const container = containerRef.current
    if (container === null) return

    let active = true
    const report = (state: BotCheckState) => {
      if (active) onChangeRef.current(state)
    }
    const restart = () => {
      const widget = widgetRef.current
      if (widget === null) return
      report(CHECKING)
      widget.api.reset(widget.id)
    }

    report(CHECKING)

    loadTurnstile().then(
      (api) => {
        if (!active) return

        const id = api.render(container, {
          sitekey: siteKey,
          language: widgetLanguage,
          theme: 'auto',
          size: 'flexible',
          appearance: 'interaction-only',
          // Retries and refreshes are done here (below), so there is exactly one of each.
          retry: 'never',
          'refresh-expired': 'manual',
          'response-field': false,
          'feedback-enabled': false,
          callback: (token) => {
            retriesRef.current = 0
            report({ status: 'passed', token })
          },
          'error-callback': () => {
            report({ status: 'failed' })
            if (retriesRef.current < AUTO_RETRIES) {
              retriesRef.current += 1
              window.clearTimeout(retryTimerRef.current)
              retryTimerRef.current = window.setTimeout(restart, RETRY_DELAY_MS)
            }
            return true
          },
          'expired-callback': restart,
          // An unsolved checkbox refreshes itself (`refresh-timeout` stays `auto`).
          'timeout-callback': () => report(CHECKING),
          'before-interactive-callback': () => report({ status: 'checking', interactive: true }),
        })

        if (typeof id === 'string') widgetRef.current = { api, id }
        else report({ status: 'failed' })
      },
      () => report({ status: 'failed' }),
    )

    return () => {
      // The token goes with the widget.
      report(CHECKING)
      active = false
      window.clearTimeout(retryTimerRef.current)
      const widget = widgetRef.current
      widgetRef.current = null
      if (widget !== null) widget.api.remove(widget.id)
    }
  }, [siteKey, widgetLanguage, attempt])

  const handledSignal = useRef(resetSignal)
  useEffect(() => {
    if (resetSignal === handledSignal.current) return
    handledSignal.current = resetSignal

    retriesRef.current = 0
    window.clearTimeout(retryTimerRef.current)

    const widget = widgetRef.current
    if (widget === null) {
      // The script never loaded (or is still on its way) — load and render again.
      setAttempt((value) => value + 1)
      return
    }
    onChangeRef.current(CHECKING)
    widget.api.reset(widget.id)
  }, [resetSignal])

  return <div ref={containerRef} />
}
