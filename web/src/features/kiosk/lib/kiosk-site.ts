/**
 * Where the public form lives (SPEC-planshet-anketa §2.3 item 1, §7, §8).
 *
 * `VITE_ANKETA_URL` is the optional form-only site (`https://anketa.bonvi.uz`),
 * set at build time once the owner adds the DNS record. The same build is
 * served on the HR site and on the form-only site; Caddy lets the form-only
 * site serve nothing but `/anketa` and `/api/kiosk/*`. `hr.bonvi.uz/anketa`
 * keeps working for the tablets that were set up on it.
 */

/** The configured short address as written, or `null` when it is not set. */
function configuredFormSite(): string | null {
  const configured = import.meta.env.VITE_ANKETA_URL?.trim()
  return configured ? configured : null
}

/** Origin of the form-only site, or `null` when it is not configured (or not a URL). */
function formSiteOrigin(): string | null {
  const configured = configuredFormSite()
  if (configured === null) return null

  try {
    return new URL(configured).origin
  } catch {
    return null
  }
}

/**
 * True when this page is open on the form-only site. Nothing there may lead
 * to an HR route: those paths are 404 on that site, and a candidate should
 * not even learn the HR system's address.
 */
export function isFormOnlySite(): boolean {
  const origin = formSiteOrigin()
  return origin !== null && origin === window.location.origin
}

/** The link HR sends to candidates: the form-only site when set, else this site's `/anketa`. */
export function publicFormUrl(): string {
  return configuredFormSite() ?? `${window.location.origin}/anketa`
}

/**
 * The one-time link that turns the office tablet into tablet mode. On the
 * form-only site when it is configured — there tablet mode cannot meet an HR
 * session at all (different origin, no HR login on it); otherwise this site's
 * own `/anketa?planshet=1`.
 */
export function tabletFormUrl(): string {
  const configured = configuredFormSite()
  if (configured === null) return `${window.location.origin}/anketa?planshet=1`

  return `${configured}${configured.includes('?') ? '&' : '?'}planshet=1`
}

/** Whether the HR dialog shows the form-only site (its explanation line differs). */
export function hasFormOnlySite(): boolean {
  return configuredFormSite() !== null
}
