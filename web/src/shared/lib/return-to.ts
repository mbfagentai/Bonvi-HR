import { useNavigate, type AnyRouter, type ParsedLocation } from '@tanstack/react-router'
import { useSyncExternalStore } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'

/**
 * ⭐ Kartadagi «← Orqaga» — HR KELGAN ro'yxatga qaytaradi.
 *
 * <b>Muammo (egasi, 2026-10-21).</b> HR «Nomzodlar» da filtr qo'yadi,
 * kartani ochadi, nomzodni rad etadi va «← Nomzodlar» ni bosadi — u
 * filtrsiz «Rad etilganlar» ga tushib qolardi. Karta qaytish manzilini
 * nomzodning HOZIRGI holatidan tuzardi (xodim kartasi esa doim yalang
 * `/xodimlar` ga), ya'ni kartada nima o'zgargani qayerga qaytishni hal
 * qilardi. To'g'risi — qayerdan KELGANI.
 *
 * <b>Qoida.</b> Router har bir o'tishni yakunlaganda (`onResolved`) joy
 * MENYUDAGI sahifami — tekshiriladi (`trackReturnTo` ga berilgan
 * `labelKeyOf`). Menyudagi sahifa bo'lsa, uning to'liq manzili (filtr,
 * qidiruv, saralash, sahifa, bo'lim — hammasi `?…` da) va menyudagi nomi
 * eslab qolinadi. Karta, kirish sahifasi va 404 menyuda yo'q — ular
 * eslanganni O'ZGARTIRMAYDI. Shu sababli kartaning bo'limlari orasida
 * yurish ham, kartadan kartaga o'tish ham (nomzod → ishga olingan xodim)
 * qaytish manzilini buzmaydi.
 *
 * <b>Brauzerning «orqaga» tugmasi</b> bunga umuman aloqasiz — u avvalgidek
 * tarix bo'yicha ishlaydi.
 *
 * <b>Nega `sessionStorage`.</b> Karta qayta yuklanganda (F5) xotira
 * tozalanadi; saqlangan nusxa «orqaga» ni o'sha ro'yxatga qaytaradi.
 * Tab bo'yicha alohida — ikkinchi tabda ochilgan ro'yxat birinchisining
 * kartasiga ta'sir qilmaydi. Karta HAVOLA orqali ochilganda (yangi tab,
 * Telegram'dagi havola, manzil satriga yozilgan) saqlangan nusxa
 * O'QILMAYDI: bu yerda «kelgan joy» yo'q va karta o'zining sukut
 * manziliga qaytaradi (`useReturnTo` ning `fallback` i).
 */

interface Remembered {
  /** To'liq ichki manzil: yo'l + `?…` (masalan `/xodimlar?status=terminated&page=2`). */
  href: string
  /** Menyudagi nomining tarjima kaliti (`nav.formerEmployees`). */
  labelKey: string
}

export interface ReturnTarget {
  /** Tugma yozuvi — allaqachon tarjima qilingan. */
  label: string
  go: () => void
}

const STORAGE_KEY = 'bonvi.returnTo'

let remembered: Remembered | null = null
const listeners = new Set<() => void>()

/**
 * Router o'tishlarini kuzatishni boshlaydi. `main.tsx` da BIR MARTA,
 * router yaratilgan zahoti chaqiriladi — birinchi yuklanish ham ushlansin.
 *
 * @param labelKeyOf Joy menyudagi sahifa bo'lsa — uning nomi kaliti,
 *   aks holda `null` (karta, kirish, 404). Menyu `app/` qatlamida turadi,
 *   bu fayl esa `shared/` da — shu sababli u tashqaridan beriladi.
 */
export function trackReturnTo(
  router: AnyRouter,
  labelKeyOf: (location: ParsedLocation) => string | null,
): () => void {
  remembered = restore()

  return router.subscribe('onResolved', ({ toLocation }) => {
    const labelKey = labelKeyOf(toLocation)
    if (labelKey === null) return

    const next: Remembered = { href: toLocation.href, labelKey }
    if (remembered?.href === next.href && remembered.labelKey === next.labelKey) return

    remembered = next
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next))
    } catch {
      // Saqlash bloklangan (maxfiy rejim) — shu sahifa ichida xotira yetadi
    }
    listeners.forEach((listener) => listener())
  })
}

/**
 * Kartadagi «orqaga» tugmasi uchun: qayerga va qanday yozuv bilan.
 *
 * HR ilova ichidagi ro'yxatdan kelgan bo'lsa — AYNAN o'sha manzilga
 * (filtrlari bilan) va tugmada o'sha sahifaning menyudagi nomi
 * («← Rad etilganlar», «← Ishdan ketganlar»). Kelgan joy bo'lmasa —
 * kartaning o'z sukut qaytishi (`fallback`).
 */
export function useReturnTo(fallback: ReturnTarget): ReturnTarget {
  const navigate = useNavigate()
  const { t } = useLocale()
  const target = useSyncExternalStore(subscribe, snapshot, snapshot)

  if (target === null) return fallback

  return {
    label: t(target.labelKey),
    go: () => void navigate({ href: target.href }),
  }
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

function snapshot(): Remembered | null {
  return remembered
}

/**
 * Saqlangan nusxa — faqat sahifa QAYTA YUKLANGANDA yoki brauzer tarixi
 * orqali qaytilganda o'qiladi. Yangi ochilishda (havola, yangi tab) u
 * boshqa ish seansidan qolgan bo'ladi — o'chiriladi.
 */
function restore(): Remembered | null {
  try {
    const entry = performance.getEntriesByType('navigation')[0] as
      | PerformanceNavigationTiming
      | undefined
    if (entry?.type !== 'reload' && entry?.type !== 'back_forward') {
      sessionStorage.removeItem(STORAGE_KEY)
      return null
    }

    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (raw === null) return null

    const value: unknown = JSON.parse(raw)
    if (typeof value !== 'object' || value === null) return null

    const { href, labelKey } = value as Record<string, unknown>
    // Faqat ilova ichidagi yo'l: `//…` yoki `https://…` boshqa saytga olib ketardi
    if (typeof href !== 'string' || !href.startsWith('/') || href.startsWith('//')) return null
    if (typeof labelKey !== 'string') return null

    return { href, labelKey }
  } catch {
    return null
  }
}
