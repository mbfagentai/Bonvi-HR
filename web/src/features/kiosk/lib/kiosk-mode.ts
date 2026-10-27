/**
 * Where the form is being filled (SPEC §7). The server cannot tell the two
 * apart and does not need to — the device decides.
 *
 * - **Personal device** (default): the candidate's own phone or computer.
 *   The draft lives in the tab for two hours, nothing resets on its own and
 *   the «Rahmat!» screen stays.
 * - **Tablet mode**: the office tablet passed from candidate to candidate.
 *   Switched on once with `/anketa?planshet=1` (off with `?planshet=0`) and
 *   remembered in `localStorage`; the first build's strict rules apply so the
 *   next person never sees the previous one's answers.
 */
export interface KioskRules {
  tablet: boolean
  /** A saved draft older than this is discarded on load. */
  draftMaxAgeMs: number
  /** Start screen: an offered draft is dropped after this long without a touch (`null` — never). */
  startIdleMs: number | null
  /** Wizard: «Davom etasizmi?» after this long without a touch (`null` — never). */
  wizardIdleMs: number | null
  /** Done screen: seconds before returning to Start (`null` — stays). */
  doneReturnSeconds: number | null
}

const PERSONAL: KioskRules = {
  tablet: false,
  draftMaxAgeMs: 2 * 60 * 60 * 1000,
  startIdleMs: null,
  wizardIdleMs: null,
  doneReturnSeconds: null,
}

const TABLET: KioskRules = {
  tablet: true,
  draftMaxAgeMs: 15 * 60 * 1000,
  startIdleMs: 2 * 60 * 1000,
  wizardIdleMs: 10 * 60 * 1000,
  doneReturnSeconds: 20,
}

const TABLET_STORAGE = 'bonvi.kiosk.tablet'

/**
 * Applies the `?planshet=` switch (when present) and returns the rules.
 * `localStorage` can be blocked (private mode): then the switch lasts only
 * while the page is open, which is still the safer default for a tablet that
 * was explicitly switched on.
 */
export function resolveKioskRules(tabletSwitch: boolean | undefined): KioskRules {
  if (tabletSwitch !== undefined) {
    try {
      if (tabletSwitch) window.localStorage.setItem(TABLET_STORAGE, '1')
      else window.localStorage.removeItem(TABLET_STORAGE)
    } catch {
      // Storage blocked — the URL switch still applies to this visit.
    }
    return tabletSwitch ? TABLET : PERSONAL
  }

  try {
    return window.localStorage.getItem(TABLET_STORAGE) === '1' ? TABLET : PERSONAL
  } catch {
    return PERSONAL
  }
}
