import type { FieldErrors } from './kiosk-draft'

/**
 * Brings the first field with an error into view (DOM order) and focuses its
 * input. A field matches its own path and the paths under it: the summary
 * line `jobs.1` stands for `jobs.1.from`. `root` limits the search to an open
 * sheet, whose fields would otherwise share paths with the line behind it.
 *
 * `block: 'center'` keeps the field clear of the pinned header and footer.
 */
export function scrollToFirstError(errors: FieldErrors, root: ParentNode = document): void {
  const paths = Object.keys(errors)
  if (paths.length === 0) return

  // Next frame: the error messages render first, then the position is measured.
  requestAnimationFrame(() => {
    const target = [...root.querySelectorAll<HTMLElement>('[data-field]')].find((element) => {
      const field = element.dataset['field'] ?? ''
      return paths.some((path) => path === field || path.startsWith(`${field}.`))
    })

    target?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    target
      ?.querySelector<HTMLElement>('input:not([type=radio]):not([type=checkbox]), textarea, select')
      ?.focus({ preventScroll: true })
  })
}

/** The sheet that is open right now, if any (`Dialog` renders `role="dialog"` into `body`). */
export function openSheet(): HTMLElement | null {
  return [...document.querySelectorAll<HTMLElement>('[role="dialog"]')].at(-1) ?? null
}
