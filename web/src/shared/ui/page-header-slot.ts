import { createContext } from 'react'

/**
 * Yuqori paneldagi ikki joy: sahifa sarlavhasi (qidiruvdan chapda) va
 * sahifa harakatlari (o'ngdagi boshqaruvlardan oldin).
 *
 * `null` — joy yo'q (tor ekran yoki qobiqsiz sahifa): `PageHeader`
 * avvalgidek sahifaning o'zida chiziladi.
 *
 * NEGA alohida fayl: React Fast Refresh bitta modulda komponent bilan
 * boshqa eksport aralashsa butun daraxtni qayta yuklaydi.
 */
export interface PageHeaderSlots {
  title: HTMLElement
  actions: HTMLElement
}

export const PageHeaderSlotContext = createContext<PageHeaderSlots | null>(null)
