import { useEffect, useRef } from 'react'

const ACTIVITY_EVENTS = ['pointerdown', 'keydown', 'input', 'touchstart', 'wheel'] as const

/**
 * Calls `onIdle` after `timeoutMs` without any touch or key press. Scrolling
 * inside the page counts too (`scroll` is captured from every element), so a
 * candidate reading a long question is not treated as gone.
 */
export function useIdle(timeoutMs: number, onIdle: () => void, enabled: boolean): void {
  const callback = useRef(onIdle)

  useEffect(() => {
    callback.current = onIdle
  }, [onIdle])

  useEffect(() => {
    if (!enabled) return

    let timer = window.setTimeout(() => callback.current(), timeoutMs)

    const reset = () => {
      window.clearTimeout(timer)
      timer = window.setTimeout(() => callback.current(), timeoutMs)
    }

    for (const name of ACTIVITY_EVENTS) window.addEventListener(name, reset, { passive: true })
    window.addEventListener('scroll', reset, { passive: true, capture: true })

    return () => {
      window.clearTimeout(timer)
      for (const name of ACTIVITY_EVENTS) window.removeEventListener(name, reset)
      window.removeEventListener('scroll', reset, { capture: true })
    }
  }, [enabled, timeoutMs])
}
