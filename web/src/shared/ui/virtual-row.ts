import { createContext, useContext, useEffect, useState } from 'react'

/**
 * Katak qanday qatorda turibdi (`DataTable`, `fill` + ko'p qator):
 *   - `off` — oddiy ro'yxat, karta, profil: hech narsa kechiktirilmaydi;
 *   - `idle` — virtual ro'yxat, aylantirish to'xtagan;
 *   - `scrolling` — virtual ro'yxat hozir aylanmoqda.
 *
 * NEGA kerak: virtual ro'yxatda qator ekranga kirgan zahoti chiziladi va
 * chiqqan zahoti o'chadi. 2 000 qatorli ro'yxat oxirigacha aylantirilsa,
 * har bir qatordagi rasm so'rovi umumiy API chegarasini (foydalanuvchiga
 * daqiqada 120 so'rov) yeb qo'yadi va HR'ning keyingi so'rovlari 429 bilan
 * qaytadi. Shu belgini ko'rgan katak og'ir so'rovini faqat ro'yxat to'xtab,
 * qator ekranda biroz turganda yuboradi (`useRowRequestReady`).
 */
export type VirtualRowState = 'off' | 'idle' | 'scrolling'

export const VirtualRowContext = createContext<VirtualRowState>('off')

/** Qator ekranda shuncha turmasa, so'rov yuborilmaydi. */
const SETTLE_MS = 300

/**
 * Katak og'ir so'rovini (rasm) hozir yuborsa bo'ladimi.
 *
 * Keshda bor rasm bunga qaramaydi — u baribir darhol chiqadi
 * (`useEmployeePhoto`, `enabled`).
 */
export function useRowRequestReady(): boolean {
  const state = useContext(VirtualRowContext)
  const settled = useSettledMount(state === 'off' ? 0 : SETTLE_MS)
  return state === 'off' || (settled && state === 'idle')
}

/** Element kamida `delayMs` davomida ekranda turdimi. */
function useSettledMount(delayMs: number): boolean {
  const [settled, setSettled] = useState(delayMs <= 0)

  useEffect(() => {
    // Kechikish olib tashlansa (ro'yxat 150 qatordan kamaydi) — darhol
    if (delayMs <= 0) {
      setSettled(true)
      return
    }
    const timer = setTimeout(() => setSettled(true), delayMs)
    return () => clearTimeout(timer)
  }, [delayMs])

  return settled
}
