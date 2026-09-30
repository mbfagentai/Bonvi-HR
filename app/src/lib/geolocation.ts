import { readStorage, writeStorage } from './storage'

/**
 * Joylashuvni o'qish — UX §C.4 dagi ketma-ketlikning aynan o'zi.
 *
 * ⚠️ Bu funksiya FAQAT tugma bosilgan hodisa ichidan chaqiriladi.
 * Safari jest bilan bog'lanmagan geolokatsiya so'rovini jim rad etadi,
 * va xodim hech narsa qilmaydigan tugmani ko'rardi. Effektdan,
 * taymerdan yoki sahifa yuklanganda — hech qachon.
 */
export interface GeoFix {
  latitude: number
  longitude: number
  accuracyMeters: number
}

export type GeoFailureKind = 'denied' | 'unavailable' | 'timeout' | 'unsupported'

export type GeoResult = { ok: true; fix: GeoFix } | { ok: false; kind: GeoFailureKind }

/** Aniqlik shundan yomon bo'lsa, bir marta jimgina qayta o'lchanadi. */
const ACCURACY_TARGET_METERS = 100
/** Ikkinchi o'lchov qancha kutadi. */
const REFINE_MS = 10_000

const ASKED_KEY = 'bonvi.ilova.geo.sorandi'

/**
 * Brauzerdan joylashuvni oladi.
 *
 * Ikki bosqich, lekin xodim uchun BITTA kutish: birinchi o'lchov ±150 m
 * bo'lib chiqsa (GPS odatda shundan boshlanadi), 10 soniyagacha eng
 * yaxshi o'lchov kutiladi. Bu serverdagi `davomat.joylashuv_aniq_emas`
 * rad javoblarining kattagina qismini xodim ko'rmasdan yo'qotadi.
 *
 * ⚠️ `maximumAge: 0` muhokama qilinmaydi: bir soat oldin UYDA olingan
 * keshlangan nuqta ofisda qabul qilingan belgi yasab berardi.
 */
export function readPosition(): Promise<GeoResult> {
  if (!('geolocation' in window.navigator)) {
    return Promise.resolve({ ok: false, kind: 'unsupported' })
  }

  markAsked()

  return new Promise<GeoResult>((resolve) => {
    window.navigator.geolocation.getCurrentPosition(
      (position) => {
        const fix = toFix(position)
        if (fix.accuracyMeters <= ACCURACY_TARGET_METERS) {
          resolve({ ok: true, fix })
          return
        }

        refine(fix, resolve)
      },
      (error) => resolve({ ok: false, kind: toFailureKind(error) }),
      { enableHighAccuracy: true, timeout: 15_000, maximumAge: 0 },
    )
  })
}

/** Ikkinchi, jim o'lchov: eng yaxshi nuqta saqlanadi. */
function refine(first: GeoFix, resolve: (result: GeoResult) => void): void {
  let best = first
  let done = false

  const finish = (): void => {
    if (done) return
    done = true
    window.clearTimeout(timer)
    window.navigator.geolocation.clearWatch(watch)
    // Nima chiqqan bo'lsa, o'sha yuboriladi: aniqlikni SERVER qayta
    // tekshiradi (SPEC §E), mijoz o'zi «yaroqsiz» deb qaror qilmaydi.
    resolve({ ok: true, fix: best })
  }

  const watch = window.navigator.geolocation.watchPosition(
    (position) => {
      const next = toFix(position)
      if (next.accuracyMeters < best.accuracyMeters) best = next
      if (best.accuracyMeters <= ACCURACY_TARGET_METERS) finish()
    },
    () => finish(),
    { enableHighAccuracy: true, timeout: REFINE_MS, maximumAge: 0 },
  )

  const timer = window.setTimeout(finish, REFINE_MS)
}

function toFix(position: GeolocationPosition): GeoFix {
  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    accuracyMeters: position.coords.accuracy,
  }
}

function toFailureKind(error: GeolocationPositionError): GeoFailureKind {
  if (error.code === 1) return 'denied'
  if (error.code === 3) return 'timeout'
  return 'unavailable'
}

export type PermissionState = 'granted' | 'denied' | 'prompt' | 'unknown'

/**
 * Ruxsat holati.
 *
 * ⚠️ `navigator.permissions` Safari'da faqat 16 dan bor, bizning pol
 * esa 12.2 — shu sababli u YO'Q deb hisoblanadi va javobi `unknown`
 * bo'ladi. Oldindan tushuntirish varag'i shuning uchun o'zining
 * `localStorage` bayrog'iga tayanadi (UX §C.4, 2-qadam).
 */
export function permissionState(): Promise<PermissionState> {
  const permissions = window.navigator.permissions

  if (!permissions || typeof permissions.query !== 'function') {
    return Promise.resolve(hasAsked() ? 'unknown' : 'prompt')
  }

  return permissions
    .query({ name: 'geolocation' as PermissionName })
    .then((status) => status.state as PermissionState)
    .catch(() => (hasAsked() ? 'unknown' : 'prompt'))
}

/** Oldindan tushuntirish varag'i kerakmi — bir martagina ko'rsatiladi. */
export function shouldPrePrompt(state: PermissionState): boolean {
  if (state === 'granted' || state === 'denied') return false
  return !hasAsked()
}

export function hasAsked(): boolean {
  return readStorage(ASKED_KEY) === '1'
}

export function markAsked(): void {
  writeStorage(ASKED_KEY, '1')
}
