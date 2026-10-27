import { createFileRoute } from '@tanstack/react-router'

import { KioskPage } from '@/features/kiosk'

/** Vacancy ids are uuids; anything else in `?vakansiya=` is ignored. */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu

interface AnketaSearch {
  /** `1` — this device is the office tablet from now on, `0` — not any more. */
  planshet?: 0 | 1
  /** Vacancy to preselect (the bot's link later, SPEC §1). */
  vakansiya?: string
}

/**
 * Public candidate questionnaire HR-F02 (`docs/SPEC-planshet-anketa.md` §7).
 *
 * Deliberately OUTSIDE `_app`: no HR auth guard, no HR shell, no key — anyone
 * with the link fills it, like a Google Form. A candidate must not see any HR
 * navigation.
 */
export const Route = createFileRoute('/anketa')({
  // The router parses `?planshet=1` as the number 1 (and keeps it a number, so the
  // address is not rewritten to `?planshet="1"`); a quoted string is accepted too.
  validateSearch: (search: Record<string, unknown>): AnketaSearch => {
    const planshet = String(search['planshet'] ?? '')
    const vakansiya = search['vakansiya']

    return {
      planshet: planshet === '1' ? 1 : planshet === '0' ? 0 : undefined,
      vakansiya: typeof vakansiya === 'string' && UUID.test(vakansiya) ? vakansiya : undefined,
    }
  },

  component: AnketaPage,
})

function AnketaPage() {
  const { planshet, vakansiya } = Route.useSearch()

  return <KioskPage tabletSwitch={planshet === undefined ? undefined : planshet === 1} vacancyId={vakansiya} />
}
