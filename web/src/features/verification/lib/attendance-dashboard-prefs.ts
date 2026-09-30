import { useCallback, useState } from 'react'

import {
  readId,
  readPageSize,
  type DashboardPin,
} from './attendance-search'

/**
 * Davomat statistikasining SHU BRAUZERDAGI shaxsiy sozlamalari:
 * eslab qolingan filtrlar («qadash» tugmasi) va ko'rinadigan ustunlar.
 *
 * NEGA serverda emas: bu ish uslubi, tizim ma'lumoti emas — boshqa
 * kompyuterda boshqacha bo'lsa hech narsa buzilmaydi. Server sozlamasi
 * esa yangi jadval, endpoint va ruxsat degani bo'lardi.
 *
 * NEGA kalitda foydalanuvchi identifikatori: bitta kompyuterda ikki HR
 * navbat bilan ishlaydi. Umumiy kalit bittasining «faqat mening
 * bo'limim» filtrini ikkinchisiga ham majburlardi.
 *
 * ⚠️ Har bir o'qish va yozish `try/catch` ichida: maxfiy rejimda yoki
 * «saytlarga ma'lumot saqlashni taqiqlash» sozlamasida `localStorage`
 * istisno tashlaydi (`auth-storage.ts` dagi izoh). Sozlama saqlanmasa
 * sahifa baribir ishlaydi — faqat keyingi safar eslab qolmaydi.
 */

/** Yashirish mumkin bo'lgan ustunlar. F.I.Sh. ATAYLAB yo'q — u doim ko'rinadi. */
export const DASHBOARD_COLUMNS = ['position', 'arrived', 'left', 'status'] as const
export type DashboardColumn = (typeof DASHBOARD_COLUMNS)[number]

const PIN_PREFIX = 'bonvi.attendance.dashboard.pin'
const COLUMNS_PREFIX = 'bonvi.attendance.dashboard.hiddenColumns'

interface Prefs {
  /** Qaysi foydalanuvchi uchun o'qilgan — almashsa qayta o'qiladi. */
  owner: string
  pin: DashboardPin | null
  hidden: readonly DashboardColumn[]
}

export interface DashboardPrefs {
  /** Eslab qolingan filtrlar; `null` — hech narsa qadalmagan. */
  pin: DashboardPin | null
  savePin: (next: DashboardPin | null) => void
  /** Yashirilgan ustunlar (ko'rinadiganlar EMAS — sabab `readHidden` da). */
  hidden: readonly DashboardColumn[]
  setColumnVisible: (column: DashboardColumn, visible: boolean) => void
}

/**
 * @param owner Foydalanuvchi identifikatori.
 */
export function useDashboardPrefs(owner: string): DashboardPrefs {
  const [prefs, setPrefs] = useState<Prefs>(() => load(owner))

  /*
    Foydalanuvchi almashsa (chiqib, boshqasi kirsa) nusxa SHU RENDERDA
    qayta o'qiladi — effektda emas: aks holda bitta kadr davomida oldingi
    odamning filtri ko'rinib, hatto qo'llanib ham ketardi. React render
    paytida o'z holatini yangilashga aynan shu holat uchun ruxsat beradi.
  */
  if (prefs.owner !== owner) setPrefs(load(owner))

  const savePin = useCallback(
    (next: DashboardPin | null) => {
      write(`${PIN_PREFIX}.${owner}`, next)
      setPrefs((current) => ({ ...current, pin: next }))
    },
    [owner],
  )

  const setColumnVisible = useCallback(
    (column: DashboardColumn, visible: boolean) => {
      setPrefs((current) => {
        const hidden = visible
          ? current.hidden.filter((item) => item !== column)
          : [...current.hidden.filter((item) => item !== column), column]

        write(`${COLUMNS_PREFIX}.${owner}`, hidden)
        return { ...current, hidden }
      })
    },
    [owner],
  )

  return { pin: prefs.pin, savePin, hidden: prefs.hidden, setColumnVisible }
}

function load(owner: string): Prefs {
  return {
    owner,
    pin: readPin(read(`${PIN_PREFIX}.${owner}`)),
    hidden: readHidden(read(`${COLUMNS_PREFIX}.${owner}`)),
  }
}

/**
 * Saqlangan filtrlar — manzil bilan BIR XIL qoida bilan tekshiriladi.
 *
 * Qo'lda buzilgan yoki eski versiyadan qolgan qiymat (masalan o'chirilgan
 * bo'limning noto'g'ri identifikatori emas, umuman identifikator emas)
 * so'rovga yetib bormaydi — manzildagi `?entity=abc` bilan aynan bir xil.
 */
function readPin(raw: unknown): DashboardPin | null {
  if (raw === null || typeof raw !== 'object' || Array.isArray(raw)) return null

  const source = raw as Record<string, unknown>
  const pin: DashboardPin = {
    entity: readId(source['entity']),
    department: readId(source['department']),
    position: readId(source['position']),
    sort: source['sort'] === '-name' ? '-name' : undefined,
    size: readPageSize(source['size']),
  }

  return Object.values(pin).some((value) => value !== undefined) ? pin : null
}

/**
 * Yashirilganlar ro'yxati saqlanadi, ko'rinadiganlar EMAS: keyinroq yangi
 * ustun qo'shilsa, u eski saqlangan sozlamasi bor foydalanuvchida ham
 * o'zi ko'rinadi. Ko'rinadiganlar saqlansa, yangi ustun hammada jimgina
 * yashirin bo'lib qolardi.
 */
function readHidden(raw: unknown): readonly DashboardColumn[] {
  if (!Array.isArray(raw)) return []
  return DASHBOARD_COLUMNS.filter((column) => raw.includes(column))
}

function read(key: string): unknown {
  try {
    const text = globalThis.localStorage.getItem(key)
    return text === null ? null : (JSON.parse(text) as unknown)
  } catch {
    // Saqlash yopiq yoki matn buzilgan — sozlama yo'q deb olinadi
    return null
  }
}

function write(key: string, value: unknown): void {
  try {
    if (value === null) globalThis.localStorage.removeItem(key)
    else globalThis.localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // Maxfiy rejim — sozlama shu seansda ishlaydi, keyingi safar eslanmaydi
  }
}
