import { useSyncExternalStore } from 'react'

/**
 * «Hozir belgilash ketyapti» bayrog'i.
 *
 * ⚠️ Faqat BITTA narsa uchun kerak: belgilash yuborilgan va javob
 * kelmagan oraliqda ilovani YANGILAB bo'lmaydi (UX §G.5). Sahifa
 * o'sha lahzada qayta yuklansa, xodim belgi qabul qilindimi yoki
 * yo'qmi bilmay qoladi — bu esa aynan §C.5 dagi noaniq holat, faqat
 * hech qanday sabab bo'lmasdan.
 */
let busy = false
const listeners: Array<() => void> = []

export function setCheckInBusy(value: boolean): void {
  if (busy === value) return
  busy = value
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void): () => void {
  listeners.push(listener)
  return () => {
    const index = listeners.indexOf(listener)
    if (index >= 0) listeners.splice(index, 1)
  }
}

function getSnapshot(): boolean {
  return busy
}

export function useCheckInBusy(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot)
}
