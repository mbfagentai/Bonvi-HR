import { z } from 'zod'

import type { LatLng } from './work-site-geometry'

/**
 * Address search through OpenStreetMap Nominatim (SPEC-joylashuvlar §D).
 *
 * Usage policy (https://operations.osmfoundation.org/policies/nominatim/):
 * at most one request per second and no autocomplete — so this is called
 * ONLY on an explicit button press / Enter, never per keystroke. The browser
 * sends the Referer header that identifies the application.
 *
 * Plain `fetch`, deliberately not the app's axios instance: that one attaches
 * the HR user's JWT, which must never leave for a third-party host.
 */
const ENDPOINT = 'https://nominatim.openstreetmap.org/search'

const hitSchema = z.object({
  lat: z.string(),
  lon: z.string(),
  display_name: z.string(),
  /** `[south, north, west, east]` as strings. */
  boundingbox: z.array(z.string()).length(4).optional(),
})

const responseSchema = z.array(hitSchema)

export interface GeocodeHit {
  point: LatLng
  label: string
  /** `[[south, west], [north, east]]` — lets the map frame a whole district, not just its centre. */
  bounds: [[number, number], [number, number]] | null
}

/** App locale → Nominatim `accept-language`. */
function nominatimLanguage(locale: string): string {
  if (locale.startsWith('uz')) return 'uz'
  if (locale.startsWith('ru')) return 'ru'
  return 'en'
}

function toBounds(box: readonly string[] | undefined): GeocodeHit['bounds'] {
  if (box === undefined) return null
  const [south, north, west, east] = box.map(Number)
  if (
    south === undefined ||
    north === undefined ||
    west === undefined ||
    east === undefined ||
    [south, north, west, east].some((value) => !Number.isFinite(value))
  ) {
    return null
  }
  return [
    [south, west],
    [north, east],
  ]
}

/** Best match inside Uzbekistan, or `null` when nothing matched. Throws on network/HTTP errors. */
export async function geocode(
  query: string,
  locale: string,
  signal?: AbortSignal,
): Promise<GeocodeHit | null> {
  const url = new URL(ENDPOINT)
  url.searchParams.set('format', 'jsonv2')
  url.searchParams.set('q', query)
  url.searchParams.set('countrycodes', 'uz')
  url.searchParams.set('limit', '1')
  url.searchParams.set('accept-language', nominatimLanguage(locale))

  const response = await fetch(url, {
    signal,
    headers: { Accept: 'application/json' },
    credentials: 'omit',
  })
  if (!response.ok) throw new Error(`nominatim: HTTP ${response.status}`)

  const hits = responseSchema.parse(await response.json())
  const first = hits[0]
  if (first === undefined) return null

  const lat = Number(first.lat)
  const lng = Number(first.lon)
  if (!Number.isFinite(lat) || !Number.isFinite(lng)) return null

  return { point: { lat, lng }, label: first.display_name, bounds: toBounds(first.boundingbox) }
}
