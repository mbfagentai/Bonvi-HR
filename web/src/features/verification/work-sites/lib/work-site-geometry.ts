/**
 * Plain geometry helpers for the work-site editor — no Leaflet here, so the
 * page chunk stays free of the map bundle.
 */

export interface LatLng {
  lat: number
  lng: number
}

/** SPEC-joylashuvlar §D: initial view when the site has no shape yet. */
export const TASHKENT: LatLng = { lat: 41.3111, lng: 69.2797 }
export const DEFAULT_ZOOM = 16

/** Zoom used when flying to a search hit or to the user's position. */
export const FOCUS_ZOOM = 17

/** Six decimals (~11 cm) — the server stores `numeric(9,6)` and rounds the same way. */
export function roundCoordinate(value: number): number {
  return Math.round(value * 1e6) / 1e6
}

export function roundPoint(point: LatLng): LatLng {
  return { lat: roundCoordinate(point.lat), lng: roundCoordinate(point.lng) }
}

export function samePoint(left: LatLng, right: LatLng): boolean {
  return left.lat === right.lat && left.lng === right.lng
}

/** «41.311100, 69.279700» — the order Google and Yandex maps accept in their search box. */
export function formatPoint(point: LatLng): string {
  return `${point.lat.toFixed(6)}, ${point.lng.toFixed(6)}`
}

/** Positive — counter-clockwise turn a → b → c. */
function cross(a: LatLng, b: LatLng, c: LatLng): number {
  return (b.lng - a.lng) * (c.lat - a.lat) - (b.lat - a.lat) * (c.lng - a.lng)
}

/** `p` lies within the bounding box of segment a–b (used only when collinear). */
function withinBox(a: LatLng, b: LatLng, p: LatLng): boolean {
  return (
    Math.min(a.lng, b.lng) <= p.lng &&
    p.lng <= Math.max(a.lng, b.lng) &&
    Math.min(a.lat, b.lat) <= p.lat &&
    p.lat <= Math.max(a.lat, b.lat)
  )
}

/** Segments a–b and c–d touch or cross (endpoints included). */
function segmentsTouch(a: LatLng, b: LatLng, c: LatLng, d: LatLng): boolean {
  const d1 = cross(c, d, a)
  const d2 = cross(c, d, b)
  const d3 = cross(a, b, c)
  const d4 = cross(a, b, d)

  if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) {
    return true
  }

  return (
    (d1 === 0 && withinBox(c, d, a)) ||
    (d2 === 0 && withinBox(c, d, b)) ||
    (d3 === 0 && withinBox(a, b, c)) ||
    (d4 === 0 && withinBox(a, b, d))
  )
}

function twiceSignedArea(ring: readonly LatLng[]): number {
  let sum = 0
  for (let index = 0; index < ring.length; index++) {
    const current = ring[index]
    const next = ring[(index + 1) % ring.length]
    if (current === undefined || next === undefined) continue
    sum += current.lng * next.lat - next.lng * current.lat
  }
  return sum
}

/**
 * Client-side preview of the server's `obyekt.polygon_kesishadi` rule
 * (`WorkSitePolygon.SelfIntersects` + zero area): non-adjacent edges must not
 * touch at all, adjacent edges must not fold back onto each other.
 *
 * Only a hint — the save button is not blocked by it; the server's answer
 * is the one shown after saving.
 */
export function polygonSelfIntersects(ring: readonly LatLng[]): boolean {
  const count = ring.length
  if (count < 3) return false

  for (let index = 0; index < count; index++) {
    const previous = ring[(index + count - 1) % count]
    const vertex = ring[index]
    const next = ring[(index + 1) % count]
    if (previous === undefined || vertex === undefined || next === undefined) continue

    // Fold-back at a shared vertex: collinear and the turn is 180°
    const dot =
      (previous.lng - vertex.lng) * (next.lng - vertex.lng) +
      (previous.lat - vertex.lat) * (next.lat - vertex.lat)
    if (cross(previous, vertex, next) === 0 && dot > 0) return true
  }

  for (let first = 0; first < count; first++) {
    for (let second = first + 1; second < count; second++) {
      const adjacent = second === first + 1 || (first === 0 && second === count - 1)
      if (adjacent) continue

      const a = ring[first]
      const b = ring[(first + 1) % count]
      const c = ring[second]
      const d = ring[(second + 1) % count]
      if (a === undefined || b === undefined || c === undefined || d === undefined) continue

      if (segmentsTouch(a, b, c, d)) return true
    }
  }

  return twiceSignedArea(ring) === 0
}

/** Whole metres from a text input; `null` when empty or not a non-negative integer. */
export function parseMeters(text: string): number | null {
  const trimmed = text.trim()
  if (!/^\d{1,6}$/u.test(trimmed)) return null
  return Number(trimmed)
}
