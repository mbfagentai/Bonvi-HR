import { useCallback, useEffect, useState, useSyncExternalStore } from 'react'

/** Ro'yxat ostidagi tanlov: 20 / 50 / 100 (+ «Barchasi»). */
export const PAGE_SIZE_OPTIONS = [20, 50, 100] as const

/** Sukut bo'yicha — egasi belgilagan (2026-10-15). */
export const DEFAULT_PAGE_SIZE = 50

/**
 * «Barchasi» — serverdagi eng katta sahifa hajmi bilan bir xil
 * (`SearchEmployeesQueryHandler.MaxPageSize` va boshqalar).
 *
 * NEGA cheksiz emas: bu son bazadagi eng katta ro'yxatdan (2026-10:
 * ~5 100 nomzod, ~2 000 xodim) ancha katta, lekin baribir chegara.
 * Yozuvlar undan oshsa, sahifalash «1 / 2» ni ko'rsatadi — ya'ni
 * «Barchasi» hech qachon jimgina yozuvni yashirmaydi.
 */
export const PAGE_SIZE_ALL = 10_000

const STORAGE_PREFIX = 'bonvi.pageSize.'

/**
 * «Barchasi» faqat ro'yxat ramkasi ishlaganda — globals.css dagi `frame`
 * varianti bilan AYNAN bir xil shart (`rem` da: brauzer shrift o'lchami
 * 16 px dan farq qilsa ham ikkisi bir xil ishlaydi). Ramkasiz jadval o'z
 * ichida aylanmaydi, ya'ni virtual qatorlar ham yo'q va ming-minglab qator
 * birdaniga chizilardi.
 */
const FRAME_QUERY = '(width >= 64rem) and (height >= 40rem)'

/**
 * Ro'yxatning sahifa hajmi — har ro'yxat uchun ALOHIDA eslab qolinadi.
 *
 * NEGA manzilda emas, brauzerda: bu foydalanuvchining odati («men
 * nomzodlarni 100 tadan ko'raman»), ro'yxatning holati emas. Manzilda
 * bo'lsa, menyudan qayta kirganda (`/nomzodlar` — `?size=` siz) har safar
 * 50 ga qaytib qolardi.
 *
 * NEGA har ro'yxatga alohida: vakansiyalarda «Barchasi» — 40 qator,
 * xodimlarda esa ming-minglab; bittasidagi tanlov ikkinchisiga
 * o'tmasligi kerak.
 *
 * @param list Ro'yxat nomi — kalit qismi (`'employees'`, `'candidates'`).
 * @param allowAll «Barchasi» ruxsat etiladimi. Chegarasiz o'sadigan
 *   jurnallarda (audit, o'tishlar) — yo'q: u yerda «hammasi» yuz
 *   minglab qator.
 *
 * @returns `pageSize` — so'rovga ketadigan haqiqiy hajm; `allowAll` —
 *   tanlovda «Barchasi» ko'rsatilsinmi (`Pagination`).
 */
export function usePageSize(list: string, allowAll: boolean) {
  const key = STORAGE_PREFIX + list

  const [stored, setStored] = useState(() => readStored(key))
  const framed = useSyncExternalStore(subscribeFrame, isFramed, () => true)
  const canShowAll = allowAll && framed

  const setPageSize = useCallback(
    (next: number) => {
      if (!isKnown(next)) return
      setStored(next)
      try {
        localStorage.setItem(key, String(next))
      } catch {
        // Maxfiy rejimda saqlab bo'lmaydi — tanlov shu tab'da ishlayveradi
      }
    },
    [key],
  )

  /*
    Saqlangan «Barchasi» o'chirilmaydi — u shunchaki hozir ishlamaydi
    (tor oyna, kartochkalar ko'rinishi) va eng katta oddiy qiymatga
    tushadi. Oyna kengaysa yoki jadvalga qaytilsa, tanlov o'zi qaytadi.
  */
  const pageSize = stored === PAGE_SIZE_ALL && !canShowAll ? PAGE_SIZE_OPTIONS[2] : stored

  return { pageSize, setPageSize, allowAll: canShowAll }
}

function readStored(key: string): number {
  try {
    const value = Number(localStorage.getItem(key))
    if (isKnown(value)) return value
  } catch {
    // Saqlash bloklangan — sukut bo'yicha qiymat
  }
  return DEFAULT_PAGE_SIZE
}

function isKnown(value: number): boolean {
  return (PAGE_SIZE_OPTIONS as readonly number[]).includes(value) || value === PAGE_SIZE_ALL
}

function subscribeFrame(onChange: () => void) {
  const media = globalThis.matchMedia?.(FRAME_QUERY)
  media?.addEventListener('change', onChange)
  return () => media?.removeEventListener('change', onChange)
}

function isFramed(): boolean {
  return globalThis.matchMedia?.(FRAME_QUERY).matches ?? true
}

/**
 * Joriy sahifa oxirgi sahifadan narida qolsa — oxirgisiga o'tkazadi.
 *
 * NEGA kerak: sahifa raqami manzilda, hajmi esa brauzerda. «Barchasi»
 * tanlanib «orqaga» bosilsa yoki birov `?page=3` havolasini yuborsa,
 * 10 000 lik hajmda 3-sahifa bo'sh — HR «topilmadi» ni ko'rardi, sahifa
 * tugmalari esa umuman chiqmasdi (`employees-page.tsx` dagi bilan bir xil
 * qoida).
 *
 * @param total Serverning HAQIQIY javobidagi son; `undefined` — javob hali
 *   kelmagan yoki eski javob ko'rsatilmoqda (`isPlaceholderData`): eski
 *   `total` ga qarab sakrab bo'lmaydi.
 */
export function useClampPage(
  page: number,
  pageSize: number,
  total: number | undefined,
  onPageChange: (page: number) => void,
) {
  useEffect(() => {
    if (total === undefined) return
    const lastPage = Math.max(1, Math.ceil(total / pageSize))
    if (page > lastPage) onPageChange(lastPage)
  }, [page, pageSize, total, onPageChange])
}
